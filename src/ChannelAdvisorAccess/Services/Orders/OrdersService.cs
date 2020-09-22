using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ChannelAdvisorAccess.Exceptions;
using ChannelAdvisorAccess.Misc;
using ChannelAdvisorAccess.OrderService;
using ChannelAdvisorAccess.Services.Items;
using Netco.Extensions;

namespace ChannelAdvisorAccess.Services.Orders
{
	/// <summary>
	/// Facade to work with CA orders.
	/// </summary>
	public class OrdersService: ServiceBaseAbstr, IOrdersService
	{
		private readonly APICredentials _credentials;
		private readonly OrderServiceSoapClient _client;
		private readonly FulfillmentService.APICredentials _fulfillmentServiceCredentials;
		private readonly FulfillmentService.FulfillmentServiceSoapClient _fulfillmentServiceClient;

		private const int MaxUnexpectedAttempt = 5;

		public string AccountId{ get; private set; }

		public string Name{ get; private set; }

		public OrdersService( APICredentials credentials, string accountName, string accountId, ObjectCache cache = null ): this( credentials, accountId, cache )
		{
			this.Name = accountName;
		}

		public OrdersService( APICredentials credentials, string accountId, ObjectCache cache = null )
		{
			this._credentials = credentials;
			this.AccountId = accountId;
			this._client = new OrderServiceSoapClient();
			this._fulfillmentServiceCredentials = new FulfillmentService.APICredentials { DeveloperKey = this._credentials.DeveloperKey, Password = this._credentials.Password };
			this._fulfillmentServiceClient = new FulfillmentService.FulfillmentServiceSoapClient();
		}

		#region Ping
		public void Ping()
		{
			AP.CreateQuery( this.AdditionalLogInfo ).Do( () =>
			{
				var result = this._client.Ping( this._credentials );
				this.CheckCaSuccess( result );
			} );
		}

		public async Task PingAsync()
		{
			await AP.CreateQueryAsync( this.AdditionalLogInfo ).Do( async () =>
			{
				var result = await this._client.PingAsync( this._credentials ).ConfigureAwait( false );
				this.CheckCaSuccess( result.PingResult );
			} ).ConfigureAwait( false );
		}
		#endregion

		#region API methods
		public IEnumerable< T > GetOrders< T >( DateTime startDate, DateTime endDate )
			where T : OrderResponseItem
		{
			var orderCriteria = new OrderCriteria
			{
				StatusUpdateFilterBeginTimeGMT = startDate,
				StatusUpdateFilterEndTimeGMT = endDate
			};

			return this.GetOrders< T >( orderCriteria );
		}

		public async Task< IEnumerable< T > > GetOrdersAsync< T >( DateTime startDate, DateTime endDate, Mark mark = null )
			where T : OrderResponseItem
		{
			var orderCriteria = new OrderCriteria
			{
				StatusUpdateFilterBeginTimeGMT = startDate,
				StatusUpdateFilterEndTimeGMT = endDate
			};

			return await this.GetOrdersAsync< T >( orderCriteria, mark ).ConfigureAwait( false );
		}

		/// <summary>
		/// Gets the orders and returns them in a list.
		/// </summary>
		/// <typeparam name="T">Type of order response.</typeparam>
		/// <param name="startDate">The start date.</param>
		/// <param name="endDate">The end date.</param>
		/// <returns>Downloads all orders matching the date and returns them in a list.</returns>
		public IList< T > GetOrdersList< T >( DateTime startDate, DateTime endDate )
			where T : OrderResponseItem
		{
			return this.GetOrders< T >( startDate, endDate ).ToList();
		}

		private readonly Dictionary< string, int > _pageSizes = new Dictionary< string, int >
		{
			{ "Low", 200 },
			{ "Medium", 100 },
			{ "High", 50 },
			{ "Complete", 20 },
		};

		/// <summary>
		/// Gets the orders.
		/// </summary>
		/// <typeparam name="T">Type of order response.</typeparam>
		/// <param name="orderCriteria">The order criteria.</param>
		/// <returns>Orders matching supplied criteria.</returns>
		public IEnumerable< T > GetOrders< T >( OrderCriteria orderCriteria )
			where T : OrderResponseItem
		{
			if( string.IsNullOrEmpty( orderCriteria.DetailLevel ) )
				orderCriteria.DetailLevel = "High";

			int pageSize;
			orderCriteria.PageSize = this._pageSizes.TryGetValue( orderCriteria.DetailLevel, out pageSize ) ? pageSize : 20;
			orderCriteria.PageNumberFilter = 1;

			var orders = new List< T >();

			while( true )
			{
				var ordersFromPage = this.GetOrdersPage( orderCriteria );

				if( ordersFromPage == null || ordersFromPage.Length == 0 )
					break;

				orders.AddRange( ordersFromPage.OfType< T >() );
				orderCriteria.PageNumberFilter += 1;
			}

			this.CheckFulfillmentStatus( orders );

			return orders;
		}

		private OrderResponseItem[] GetOrdersPage( OrderCriteria orderCriteria )
		{
			return AP.CreateQuery( this.AdditionalLogInfo ).Get( () =>
			{
				var results = this._client.GetOrderList( this._credentials, this.AccountId, orderCriteria );
				CheckCaSuccess( results );
				var resultData = results.ResultData ?? new OrderResponseItem[ 0 ];

				// If you get message code = 1 (Unexpected)
				if( results.MessageCode == 1 )
					resultData = this.HandleErrorUnexpected( orderCriteria );

				return resultData;
			} );
		}

		private OrderResponseItem[] HandleErrorUnexpected( OrderCriteria orderCriteria, [ CallerMemberName ] string callerMemberName = "" )
		{
			var result = new List< OrderResponseItem >();
			var prevPageSize = orderCriteria.PageSize;
			var prevPageNumber = orderCriteria.PageNumberFilter;
			var pageNumberBy1 = prevPageSize * ( prevPageNumber - 1 );
			for( var i = 1; i <= prevPageSize; i++ )
			{
				orderCriteria.PageSize = 1;
				orderCriteria.PageNumberFilter = pageNumberBy1 + i;
				var numberAttempt = 0;
				while( numberAttempt < MaxUnexpectedAttempt )
				{
					var answer = this._client.GetOrderList( this._credentials, this.AccountId, orderCriteria );
					if( answer.Status == ResultStatus.Success )
					{
						result.AddRange( answer.ResultData );
						break;
					}
					
					numberAttempt++;
					this.LogUnexpectedError( orderCriteria, answer, pageNumberBy1 + i, callerMemberName, numberAttempt );
					this.DoDelayUnexpectedAsync( new TimeSpan( 0, 2, 0 ), numberAttempt ).Wait();
				}
			}

			orderCriteria.PageSize = prevPageSize;
			orderCriteria.PageNumberFilter = prevPageNumber;

			return result.ToArray();
		}

		/// <summary>
		/// Gets the orders.
		/// </summary>
		/// <typeparam name="T">Type of order response.</typeparam>
		/// <param name="orderCriteria">The order criteria.</param>
		/// <param name="mark"></param>
		/// <returns>Orders matching supplied criteria.</returns>
		public async Task< IEnumerable< T > > GetOrdersAsync< T >( OrderCriteria orderCriteria, Mark mark = null )
			where T : OrderResponseItem
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : orderCriteria.ToJson() ) );

				if( string.IsNullOrEmpty( orderCriteria.DetailLevel ) )
					orderCriteria.DetailLevel = "High";

				int pageSize;
				orderCriteria.PageSize = this._pageSizes.TryGetValue( orderCriteria.DetailLevel, out pageSize ) ? pageSize : 20;
				orderCriteria.PageNumberFilter = 1;

				var orders = new List< T >();
				while( true )
				{
					var ordersFromPage = await this.GetOrdersPageAsync( orderCriteria, mark ).ConfigureAwait( false );

					if( ordersFromPage == null || ordersFromPage.Length == 0 )
						break;

					ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark: mark, methodResult: ordersFromPage.OfType< T >().ToJson(), additionalInfo: "\"Result of ordersFromPage.OfType< T >() for page\"" ) );
					orders.AddRange( ordersFromPage.OfType< T >() );
					orderCriteria.PageNumberFilter += 1;
				}

				await this.CheckFulfillmentStatusAsync( orders, mark );

				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : orders.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : orderCriteria.ToJson() ) );
				return orders;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		private async Task< OrderResponseItem[] > GetOrdersPageAsync( OrderCriteria orderCriteria, Mark mark = null )
		{
			return await AP.CreateQueryAsync( this.AdditionalLogInfo, mark ).Get( async () =>
			{
				var results = await this._client.GetOrderListAsync( this._credentials, this.AccountId, orderCriteria ).ConfigureAwait( false );
				CheckCaSuccess( results.GetOrderListResult );
				var resultData = results.GetOrderListResult.ResultData ?? new OrderResponseItem[ 0 ];

				// If you get message code = 1 (Unexpected)
				if( results.GetOrderListResult.MessageCode == 1 )
					resultData = await this.HandleErrorUnexpectedAsync( mark, orderCriteria ).ConfigureAwait( false );

				return resultData;
			} ).ConfigureAwait( false );
		}

		private async Task< OrderResponseItem[] > HandleErrorUnexpectedAsync( Mark mark, OrderCriteria orderCriteria, [ CallerMemberName ] string callerMemberName = "" )
		{
			var result = new List< OrderResponseItem >();
			var prevPageSize = orderCriteria.PageSize;
			var prevPageNumber = orderCriteria.PageNumberFilter;
			var pageNumberBy1 = prevPageSize * ( prevPageNumber - 1 );
			for( var i = 1; i <= prevPageSize; i++ )
			{
				orderCriteria.PageSize = 1;
				orderCriteria.PageNumberFilter = pageNumberBy1 + i;
				var numberAttempt = 0;
				while( numberAttempt < MaxUnexpectedAttempt )
				{
					ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : orderCriteria.ToJson() ) );
					var answer = await this._client.GetOrderListAsync( this._credentials, this.AccountId, orderCriteria ).ConfigureAwait( false );
					ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : answer.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : orderCriteria.ToJson() ) );
					if( answer.GetOrderListResult.Status == ResultStatus.Success )
					{
						result.AddRange( answer.GetOrderListResult.ResultData );
						break;
					}

					numberAttempt++;
					this.LogUnexpectedError( orderCriteria, answer.GetOrderListResult, orderCriteria.PageNumberFilter, callerMemberName, numberAttempt );
					await this.DoDelayUnexpectedAsync( new TimeSpan( 0, 2, 0 ), numberAttempt ).ConfigureAwait( false );
				}
			}

			orderCriteria.PageSize = prevPageSize;
			orderCriteria.PageNumberFilter = prevPageNumber;

			ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark: mark, methodResult: result.Select( o => o.OrderID ).ToJson(), additionalInfo: "\"Final result after looping\"", methodParameters: orderCriteria.ToJson() ) );
			return result.ToArray();
		}

		/// <summary>
		/// Submits the order.
		/// </summary>
		/// <param name="orderSubmit">The order submit.</param>
		/// <returns>New order CA id.</returns>
		public int SubmitOrder( OrderSubmit orderSubmit )
		{
			return AP.CreateSubmit( this.AdditionalLogInfo ).Get( () =>
			{
				var apiResults = this._client.SubmitOrder( this._credentials, this.AccountId, orderSubmit );
				this.CheckCaSuccess( apiResults );
				return apiResults.ResultData;
			} );
		}

		public async Task< int > SubmitOrderAsync( OrderSubmit orderSubmit )
		{
			return await AP.CreateSubmitAsync( this.AdditionalLogInfo ).Get( async () =>
			{
				var apiResults = await this._client.SubmitOrderAsync( this._credentials, this.AccountId, orderSubmit ).ConfigureAwait( false );
				this.CheckCaSuccess( apiResults.SubmitOrderResult );
				return apiResults.SubmitOrderResult.ResultData;
			} ).ConfigureAwait( false );
		}
		#endregion

		#region CheckFulfillmentStatus
		private void CheckFulfillmentStatus< T >( List< T > orders, Mark mark = null ) where T : OrderResponseItem
		{
			var refundedOrderIds = GetRefundedOrderIds( orders );

			if( refundedOrderIds.Count == 0 )
				return;

			int pageSize;
			if( !this._pageSizes.TryGetValue( "High", out pageSize ) )
				pageSize = 50;

			var cancelledOrderIds = new List< int >();
			var ordersParts = ItemsService.ToChunks( refundedOrderIds, pageSize );

			ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : refundedOrderIds.ToJson() ) );
			foreach( var part in ordersParts )
			{
				var ordersFulfillment = AP.CreateQuery( this.AdditionalLogInfo, mark ).Get( () =>
				{
					var results = this._fulfillmentServiceClient.GetOrderFulfillmentDetailList( this._fulfillmentServiceCredentials, this.AccountId, part.ToArray(), null );
					CheckCaSuccess( results );
					var resultData = results.ResultData ?? new FulfillmentService.OrderFulfillmentResponse[ 0 ];

					return resultData;
				} );

				ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : ordersFulfillment.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : part.ToJson() ) );

				var cancelledOrderIdsPart = ordersFulfillment.Where( o => o.FulfillmentList.All( fulfillment => fulfillment.FulfillmentStatus == "Canceled" ) ).Select( o => o.OrderID );
				cancelledOrderIds.AddRange( cancelledOrderIdsPart );
			}
			ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : cancelledOrderIds.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : refundedOrderIds.ToJson() ) );

			CancelOrders( orders, cancelledOrderIds );
		}

		private async Task CheckFulfillmentStatusAsync< T >( List< T > orders, Mark mark = null ) where T : OrderResponseItem
		{
			var refundedOrderIds = GetRefundedOrderIds( orders );

			if( refundedOrderIds.Count == 0 )
				return;

			int pageSize;
			if( !this._pageSizes.TryGetValue( "High", out pageSize ) )
				pageSize = 50;

			var ordersParts = ItemsService.ToChunks( refundedOrderIds, pageSize );

			ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : refundedOrderIds.ToJson() ) );
			var cancelledOrderIds = ( await ordersParts.ProcessInBatchAsync( 3, async part =>
			{
				var ordersFulfillment = await AP.CreateQueryAsync( this.AdditionalLogInfo, mark ).Get( async () =>
				{
					var results = await this._fulfillmentServiceClient.GetOrderFulfillmentDetailListAsync( this._fulfillmentServiceCredentials, this.AccountId, part.ToArray(), null ).ConfigureAwait( false );
					CheckCaSuccess( results.GetOrderFulfillmentDetailListResult );
					var resultData = results.GetOrderFulfillmentDetailListResult.ResultData ?? new FulfillmentService.OrderFulfillmentResponse[ 0 ];

					return resultData;
				} ).ConfigureAwait( false );

				ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : ordersFulfillment.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : part.ToJson() ) );

				return ordersFulfillment.Where( o => o.FulfillmentList.All( fulfillment => fulfillment.FulfillmentStatus == "Canceled" ) ).Select( o => o.OrderID );
			} ).ConfigureAwait( false ) ).SelectMany( x => x ).ToArray();

			ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : cancelledOrderIds.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : refundedOrderIds.ToJson() ) );

			CancelOrders( orders, cancelledOrderIds );
		}

		private static void CancelOrders< T >( IEnumerable< T > orders, IEnumerable< int > cancelledOrderIds ) where T : OrderResponseItem
		{
			if( !cancelledOrderIds.Any() )
				return;

			foreach( var order in orders )
			{
				if( cancelledOrderIds.Contains( order.OrderID ) )
				{
					order.OrderState = "Cancelled";
				}
			}
		}

		private static List< int > GetRefundedOrderIds< T >( IEnumerable< T > orders ) where T : OrderResponseItem
		{
			var refundedOrders = new List< int >();
			foreach( var order in orders.Where( o => o.OrderState != "Cancelled" ) )
			{
				var typedOrder = order as OrderResponseDetailLow;
				if( typedOrder == null )
					continue;
				var refundStatus = typedOrder.OrderStatus.OrderRefundStatus;
				if( refundStatus == "OrderLevel" || refundStatus == "LineItemLevel" || refundStatus == "OrderAndLineItemLevel" )
					refundedOrders.Add( order.OrderID );
			}
			return refundedOrders;
		}
		#endregion

		public IEnumerable< OrderUpdateResponse > UpdateOrderList( OrderUpdateSubmit[] orderUpdates )
		{
			return AP.CreateSubmit( this.AdditionalLogInfo ).Get( () =>
			{
				var results = this._client.UpdateOrderList( this._credentials, this.AccountId, orderUpdates );
				this.CheckCaSuccess( results );
				return results.ResultData;
			} );
		}

		public async Task< IEnumerable< OrderUpdateResponse > > UpdateOrderListAsync( OrderUpdateSubmit[] orderUpdates )
		{
			return await AP.CreateSubmitAsync( this.AdditionalLogInfo ).Get( async () =>
			{
				var results = await this._client.UpdateOrderListAsync( this._credentials, this.AccountId, orderUpdates ).ConfigureAwait( false );
				this.CheckCaSuccess( results.UpdateOrderListResult );
				return results.UpdateOrderListResult.ResultData;
			} ).ConfigureAwait( false );
		}

		private static void CheckCaSuccess( APIResultOfArrayOfOrderResponseItem orderList )
		{
			if( orderList.Status != ResultStatus.Success )
			{
				if( orderList.MessageCode != 1 )
					throw new ChannelAdvisorException( orderList.MessageCode, orderList.Message );
			}
		}

		private static void CheckCaSuccess( FulfillmentService.APIResultOfArrayOfOrderFulfillmentResponse response )
		{
			if( response.Status != FulfillmentService.ResultStatus.Success )
				throw new ChannelAdvisorException( response.MessageCode, response.Message );
		}

		private void CheckCaSuccess( APIResultOfInt32 results )
		{
			if( results.Status != ResultStatus.Success )
				throw new ChannelAdvisorException( results.MessageCode, results.Message );
		}

		private void CheckCaSuccess( APIResultOfArrayOfOrderUpdateResponse results )
		{
			if( results.Status != ResultStatus.Success )
				throw new ChannelAdvisorException( results.MessageCode, results.Message );
		}

		private void CheckCaSuccess( APIResultOfString result )
		{
			if( result.Status != ResultStatus.Success )
				throw new ChannelAdvisorException( result.MessageCode, result.Message );
		}

		private void LogUnexpectedError( OrderCriteria orderCriteria, APIResultOfArrayOfOrderResponseItem orderList, int? pageNumberBy1, string callerMemberName, int attempt )
		{
			var callInfo = new CallInfoBasic( connectionInfo: this.ToJson(), additionalInfo: this.AdditionalLogInfo(), memberName: callerMemberName );
			var criteria = string.Format( "StatusUpdateFilterBeginTimeGMTField: {0}. StatusUpdateFilterEndTimeGMTField: {1}. Number in sequence for pageSize equal 1 : {2}", orderCriteria.StatusUpdateFilterBeginTimeGMT.GetValueOrDefault(), orderCriteria.StatusUpdateFilterEndTimeGMT.GetValueOrDefault(), pageNumberBy1 );
			var message = "Unexpected Error. Attempt: {0}. Criteria: {1}".FormatWith( attempt, criteria );
			var exception = new ChannelAdvisorException( orderList.MessageCode, message );
			ChannelAdvisorLogger.LogTraceException( callInfo, exception, message );
		}

		public async Task DoDelayUnexpectedAsync( TimeSpan time, int attempt )
		{
			string message = string.Format( @"Wait by reason of error 1 (Unexpected) {0} minute(s). Attempt: {1}", time.Minutes, attempt );
			var callInfo = new CallInfoBasic( connectionInfo: this.ToJson(), additionalInfo: this.AdditionalLogInfo() );
			ChannelAdvisorLogger.LogTraceFailure( message, callInfo );
			await Task.Delay( time ).ConfigureAwait( false );
		}
	}
}