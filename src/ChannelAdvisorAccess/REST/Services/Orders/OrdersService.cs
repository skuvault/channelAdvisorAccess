using ChannelAdvisorAccess.Exceptions;
using ChannelAdvisorAccess.Misc;
using ChannelAdvisorAccess.OrderService;
using ChannelAdvisorAccess.Services.Orders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChannelAdvisorAccess.REST.Models.Configuration;
using ChannelAdvisorAccess.REST.Shared;
using ChannelAdvisorAccess.REST.Models;
using ChannelAdvisorAccess.Services.Items;

namespace ChannelAdvisorAccess.REST.Services.Orders
{
	/// <summary>
	/// Facade to work with ChannelAdvisor REST API
	/// </summary>
	public class OrdersService : RestServiceBaseAbstr, IOrdersService
	{
		private readonly IItemsService _itemsService;

		/// <summary>
		///	Rest service with standard authorization flow
		/// </summary>
		/// <param name="credentials">Rest application credentials</param>
		/// <param name="accountName">Tenant account name</param>
		/// <param name="accessToken">Tenant access token</param>
		/// <param name="refreshToken">Tenant refresh token</param>
		/// <param name="itemsService">Items service (to get Distribution Centers)</param>
		public OrdersService( RestCredentials credentials, string accountName, string accessToken, string refreshToken, IItemsService itemsService ) 
			: base( credentials, accountName, accessToken, refreshToken ) 
		{ 
			this._itemsService = itemsService;		
		}

		/// <summary>
		///	Rest service with soap compatible authorization flow
		/// </summary>
		/// <param name="credentials">Rest application credentials</param>
		/// <param name="soapCredentials">Soap application credentials</param>
		/// <param name="accountId">Tenant account id</param>
		/// <param name="accountName">Tenant account name</param>
		/// <param name="itemsService">Items service (to get Distribution Centers)</param>
		public OrdersService( RestCredentials credentials, APICredentials soapCredentials, string accountId, string accountName, IItemsService itemsService ) 
			: base( credentials, soapCredentials, accountId, accountName ) 
		{
			this._itemsService = itemsService;
		}

		/// <summary>
		///	Gets orders by created date range
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="startDate"></param>
		/// <param name="endDate"></param>
		/// <returns></returns>
		public IEnumerable< T > GetOrders< T >( DateTime startDate, DateTime endDate ) where T : OrderResponseItem
		{
			var criteria = new OrderCriteria()
			{
				StatusUpdateFilterBeginTimeGMT = startDate,
				StatusUpdateFilterEndTimeGMT = endDate,
			};

			return this.GetOrders< T >( criteria );
		}

		/// <summary>
		///	Gets orders by complex query
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="orderCriteria"></param>
		/// <returns></returns>
		public IEnumerable< T > GetOrders< T >( OrderCriteria orderCriteria ) where T : OrderResponseItem
		{
			return this.GetOrdersAsync< T >( orderCriteria ).GetAwaiter().GetResult();
		}

		/// <summary>
		///	Gets orders and return it as list
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="startDate"></param>
		/// <param name="endDate"></param>
		/// <returns></returns>
		public IList< T > GetOrdersList< T >( DateTime startDate, DateTime endDate ) where T : OrderResponseItem
		{
			return this.GetOrders< T >( startDate, endDate ).ToList();
		}

		/// <summary>
		///	Gets orders asynchronously by created date range
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="startDate"></param>
		/// <param name="endDate"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public Task< IEnumerable < T > > GetOrdersAsync< T >( DateTime startDate, DateTime endDate, Mark mark = null ) where T : OrderResponseItem
		{
			OrderCriteria criteria = new OrderCriteria()
			{
				StatusUpdateFilterBeginTimeGMT = startDate,
				StatusUpdateFilterEndTimeGMT = endDate,
			};

			return this.GetOrdersAsync< T >( criteria, mark );
		}

		/// <summary>
		///	Gets orders asynchronously by complex query
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="orderCriteria"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task< IEnumerable< T > > GetOrdersAsync< T >( OrderCriteria orderCriteria, Mark mark = null ) where T : OrderResponseItem
		{
			if ( orderCriteria.OrderIDList != null && orderCriteria.OrderIDList.Length > 0 )
			{
				var result = new List< T >();

				foreach( int orderId in orderCriteria.OrderIDList )
				{
					var searchOrderFilter = this.GetRequestFilterString( orderCriteria, orderId );
					var orders = await this.GetOrdersAsync< T >( searchOrderFilter, mark ).ConfigureAwait( false );
					result.AddRange( orders );
				}
			
				return result;
			}

			var filter = this.GetRequestFilterString( orderCriteria );
			return await this.GetOrdersAsync< T >( filter, mark ).ConfigureAwait( false );
		}

		/// <summary>
		///	Gets orders asynchronously and returns it as list
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="filter"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task< IEnumerable< T > > GetOrdersAsync < T >( string filter, Mark mark )
		{
			var orders = new List< T >();

			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			var url = ChannelAdvisorEndPoint.OrdersUrl + "?$expand=Items($expand=Promotions),Fulfillments($expand=Items),Adjustments,CustomFields";

			if ( !string.IsNullOrEmpty( filter )) 
				url += "&$filter=" + filter;

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark: mark, additionalInfo: this.AdditionalLogInfo(), methodParameters: filter ) );

				var result = await this.GetResponseAsync< Models.Order >( url, mark ).ConfigureAwait( false );

				var distributionCenters = new DistributionCenter[] { };
				if ( result.Response.Count() > 0 )
				{
					distributionCenters = await this._itemsService.GetDistributionCentersAsync().ConfigureAwait( false );
				}

				orders.AddRange( result.Response.Select( order => order.ToOrderResponseDetailComplete( distributionCenters ) ).OfType< T >() );

				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark: mark, methodResult: orders.ToJson(), additionalInfo: this.AdditionalLogInfo(), methodParameters: filter ) );
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception, methodParameters: filter );
			}

			return orders;
		}

		/// <summary>
		///	Gets filtering parameter for REST GET request
		/// </summary>
		/// <param name="criteria"></param>
		/// <param name="orderId">Order id</param>
		/// <returns></returns>
		private string GetRequestFilterString( OrderCriteria criteria, int? orderId = null )
		{
			List< string > clauses = new List< string >();
			
			if ( criteria.OrderCreationFilterBeginTimeGMT.HasValue )
				clauses.Add( String.Format( "CreatedDateUtc ge {0} and ", base.ConvertDate( criteria.OrderCreationFilterBeginTimeGMT.Value ) ) );

			if ( criteria.OrderCreationFilterEndTimeGMT.HasValue )
				clauses.Add( String.Format( "CreatedDateUtc le {0} and ", base.ConvertDate( criteria.OrderCreationFilterEndTimeGMT.Value ) ) );

			if ( criteria.StatusUpdateFilterBeginTimeGMT.HasValue
				&& criteria.StatusUpdateFilterEndTimeGMT.HasValue )
				clauses.Add( String.Format( "(CheckoutDateUtc ge {0} and CheckoutDateUtc le {1}) or (PaymentDateUtc ge {0} and PaymentDateUtc le {1}) or (ShippingDateUtc ge {0} and ShippingDateUtc le {1}) and ", base.ConvertDate( criteria.StatusUpdateFilterBeginTimeGMT.Value ), base.ConvertDate( criteria.StatusUpdateFilterEndTimeGMT.Value ) ) );

			if ( orderId != null )
				clauses.Add( String.Format( "ID eq {0} and ", orderId.Value.ToString() ) );

			if ( clauses.Count > 0 )
				clauses[ clauses.Count - 1] = clauses.Last().Substring( 0, clauses.Last().LastIndexOf( "and" ) );

			return string.Join( " ", clauses );
		}

		public void Ping()
		{
			throw new NotImplementedException();
		}

		public Task PingAsync()
		{
			throw new NotImplementedException();
		}

		public int SubmitOrder(OrderSubmit orderSubmit)
		{
			throw new NotImplementedException();
		}

		public Task<int> SubmitOrderAsync(OrderSubmit orderSubmit)
		{
			throw new NotImplementedException();
		}

		public IEnumerable<OrderUpdateResponse> UpdateOrderList(OrderUpdateSubmit[] orderUpdates)
		{
			throw new NotImplementedException();
		}

		public Task<IEnumerable<OrderUpdateResponse>> UpdateOrderListAsync(OrderUpdateSubmit[] orderUpdates)
		{
			throw new NotImplementedException();
		}
	}
}
