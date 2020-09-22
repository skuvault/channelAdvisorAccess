using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ChannelAdvisorAccess.Exceptions;
using ChannelAdvisorAccess.Misc;
using ChannelAdvisorAccess.ShippingService;
using Netco.Extensions;
using Netco.Logging;
using Newtonsoft.Json;

namespace ChannelAdvisorAccess.Services.Shipping
{
	public class ShippingService: IShippingService
	{
		private readonly APICredentials _credentials;
		private readonly ShippingServiceSoapClient _client;

		[ JsonIgnore ]
		public Func< string > AdditionalLogInfo{ get; set; }

		public ShippingService( APICredentials credentials, string accountId )
		{
			this._credentials = credentials;
			this.AccountId = accountId;
			this._client = new ShippingServiceSoapClient();
		}

		public ShippingService( APICredentials credentials, string name, string id ): this( credentials, id )
		{
			this.Name = name;
		}

		public string Name{ get; private set; }
		public string AccountId{ get; private set; }

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

		#region Mark Order Shipped
		public void MarkOrderShipped( int orderId, string carrierCode, string classCode, string trackingNumber, DateTime dateShipped )
		{
			try
			{
				AP.CreateSubmit( this.AdditionalLogInfo ).Do( () =>
				{
					var result = this._client.SubmitOrderShipmentList( this._credentials, this.AccountId, CreateShipmentByOrderId( orderId, carrierCode, classCode, trackingNumber, dateShipped ) );
					this.CheckCaSuccess( result );
				} );
			}
			catch( Exception e )
			{
				throw new MarkOrderShippedException( orderId.ToString( CultureInfo.InvariantCulture ), this.AccountId, carrierCode, classCode, trackingNumber, e );
			}
		}

		public async Task MarkOrderShippedAsync( int orderId, string carrierCode, string classCode, string trackingNumber, DateTime dateShipped )
		{
			try
			{
				await AP.CreateSubmitAsync( this.AdditionalLogInfo ).Do( async () =>
				{
					var result = await this._client.SubmitOrderShipmentListAsync( this._credentials, this.AccountId, CreateShipmentByOrderId( orderId, carrierCode, classCode, trackingNumber, dateShipped ) ).ConfigureAwait( false );
					this.CheckCaSuccess( result.SubmitOrderShipmentListResult );
				} ).ConfigureAwait( false );
			}
			catch( Exception e )
			{
				throw new MarkOrderShippedException( orderId.ToString( CultureInfo.InvariantCulture ), this.AccountId, carrierCode, classCode, trackingNumber, e );
			}
		}

		private static OrderShipment[] CreateShipmentByOrderId( int orderId, string carrierCode, string classCode, string trackingNumber, DateTime dateShipped )
		{
			return new[]
			{
				new OrderShipment
				{
					OrderID = orderId,
					ShipmentType = "Full",
					FullShipment = new FullShipmentContents
					{
						CarrierCode = carrierCode,
						ClassCode = classCode,
						DateShippedGMT = dateShipped.ToUniversalTime(),
						TrackingNumber = trackingNumber
					}
				}
			};
		}

		public void MarkOrderShipped( string clientOrderId, string carrierCode, string classCode, string trackingNumber, DateTime dateShipped )
		{
			try
			{
				AP.CreateSubmit( this.AdditionalLogInfo ).Do( () =>
				{
					var result = this._client.SubmitOrderShipmentList( this._credentials, this.AccountId, CreateShipmentByClientId( clientOrderId, carrierCode, classCode, trackingNumber, dateShipped ) );
					this.CheckCaSuccess( result );
				} );
			}
			catch( Exception e )
			{
				throw new MarkOrderShippedException( clientOrderId, this.AccountId, carrierCode, classCode, trackingNumber, e );
			}
		}

		public async Task MarkOrderShippedAsync( string clientOrderId, string carrierCode, string classCode, string trackingNumber, DateTime dateShipped )
		{
			try
			{
				await AP.CreateSubmitAsync( this.AdditionalLogInfo ).Do( async () =>
				{
					var result = await this._client.SubmitOrderShipmentListAsync( this._credentials, this.AccountId, CreateShipmentByClientId( clientOrderId, carrierCode, classCode, trackingNumber, dateShipped ) ).ConfigureAwait( false );
					this.CheckCaSuccess( result.SubmitOrderShipmentListResult );
				} ).ConfigureAwait( false );
			}
			catch( Exception e )
			{
				throw new MarkOrderShippedException( clientOrderId, this.AccountId, carrierCode, classCode, trackingNumber, e );
			}
		}

		private static OrderShipment[] CreateShipmentByClientId( string clientOrderId, string carrierCode, string classCode, string trackingNumber, DateTime dateShipped )
		{
			return new[]
			{
				new OrderShipment
				{
					ClientOrderIdentifier = clientOrderId,
					ShipmentType = "Full",
					FullShipment = new FullShipmentContents
					{
						CarrierCode = carrierCode,
						ClassCode = classCode,
						DateShippedGMT = dateShipped.ToUniversalTime(),
						TrackingNumber = trackingNumber
					}
				}
			};
		}

		public void MarkOrderShipped( int orderId, PartialShipmentContents partialShipmentContents )
		{
			try
			{
				AP.CreateSubmit( this.AdditionalLogInfo ).Do( () =>
				{
					var result = this._client.SubmitOrderShipmentList( this._credentials, this.AccountId, CreatePartialShipmentByOrderId( orderId, partialShipmentContents ) );
					this.CheckCaSuccess( result );
				} );
			}
			catch( Exception e )
			{
				throw new MarkOrderShippedException( orderId.ToString( CultureInfo.InvariantCulture ), this.AccountId, partialShipmentContents.CarrierCode, partialShipmentContents.ClassCode, partialShipmentContents.TrackingNumber, e );
			}
		}

		public async Task MarkOrderShippedAsync( int orderId, PartialShipmentContents partialShipmentContents )
		{
			try
			{
				await AP.CreateSubmitAsync( this.AdditionalLogInfo ).Do( async () =>
				{
					var result = await this._client.SubmitOrderShipmentListAsync( this._credentials, this.AccountId, CreatePartialShipmentByOrderId( orderId, partialShipmentContents ) ).ConfigureAwait( false );
					this.CheckCaSuccess( result.SubmitOrderShipmentListResult );
				} ).ConfigureAwait( false );
			}
			catch( Exception e )
			{
				throw new MarkOrderShippedException( orderId.ToString( CultureInfo.InvariantCulture ), this.AccountId, partialShipmentContents.CarrierCode, partialShipmentContents.ClassCode, partialShipmentContents.TrackingNumber, e );
			}
		}

		private static OrderShipment[] CreatePartialShipmentByOrderId( int orderId, PartialShipmentContents partialShipmentContents )
		{
			return new[]
			{
				new OrderShipment
				{
					OrderID = orderId,
					ShipmentType = "Partial",
					PartialShipment = partialShipmentContents
				}
			};
		}

		public void MarkOrderShipped( string clientOrderId, PartialShipmentContents partialShipmentContents )
		{
			try
			{
				AP.CreateSubmit( this.AdditionalLogInfo ).Do( () =>
				{
					var result = this._client.SubmitOrderShipmentList( this._credentials, this.AccountId, CreatePartialShipmentByClientId( clientOrderId, partialShipmentContents ) );
					this.CheckCaSuccess( result );
				} );
			}
			catch( Exception e )
			{
				throw new MarkOrderShippedException( clientOrderId, this.AccountId, partialShipmentContents.CarrierCode, partialShipmentContents.ClassCode, partialShipmentContents.TrackingNumber, e );
			}
		}

		public async Task MarkOrderShippedAsync( string clientOrderId, PartialShipmentContents partialShipmentContents )
		{
			try
			{
				await AP.CreateSubmitAsync( this.AdditionalLogInfo ).Do( async () =>
				{
					var result = await this._client.SubmitOrderShipmentListAsync( this._credentials, this.AccountId, CreatePartialShipmentByClientId( clientOrderId, partialShipmentContents ) ).ConfigureAwait( false );
					this.CheckCaSuccess( result.SubmitOrderShipmentListResult );
				} ).ConfigureAwait( false );
			}
			catch( Exception e )
			{
				throw new MarkOrderShippedException( clientOrderId, this.AccountId, partialShipmentContents.CarrierCode, partialShipmentContents.ClassCode, partialShipmentContents.TrackingNumber, e );
			}
		}

		private static OrderShipment[] CreatePartialShipmentByClientId( string clientOrderId, PartialShipmentContents partialShipmentContents )
		{
			return new[]
			{
				new OrderShipment
				{
					ClientOrderIdentifier = clientOrderId,
					ShipmentType = "Partial",
					PartialShipment = partialShipmentContents
				}
			};
		}
		#endregion

		public void SubmitOrderShipmentList( IEnumerable< OrderShipment > orderShipments )
		{
			orderShipments.DoWithPages( 50, p => AP.CreateSubmit( this.AdditionalLogInfo ).Do( () =>
			{
				var result = this._client.SubmitOrderShipmentList( this._credentials, this.AccountId, p.ToArray() );
				this.CheckCaSuccess( result );
			} ) );
		}

		public async Task SubmitOrderShipmentListAsync( IEnumerable< OrderShipment > orderShipments )
		{
			await orderShipments.DoWithPagesAsync( 50, async p => await AP.CreateSubmitAsync( this.AdditionalLogInfo ).Do( async () =>
			{
				var result = await this._client.SubmitOrderShipmentListAsync( this._credentials, this.AccountId, p.ToArray() ).ConfigureAwait( false );
				this.CheckCaSuccess( result.SubmitOrderShipmentListResult );
			} ).ConfigureAwait( false ) ).ConfigureAwait( false );
		}

		public OrderShipmentHistoryResponse[] GetOrderShipmentHistoryList( int[] orderIdList )
		{
			return this.GetOrderShipmentHistoryList( orderIdList, new string[] { } );
		}

		public async Task< OrderShipmentHistoryResponse[] > GetOrderShipmentHistoryListAsync( int[] orderIdList )
		{
			return await this.GetOrderShipmentHistoryListAsync( orderIdList, new string[] { } ).ConfigureAwait( false );
		}

		public OrderShipmentHistoryResponse[] GetOrderShipmentHistoryList( string[] clientOrderIdentifierList )
		{
			return this.GetOrderShipmentHistoryList( new int[] { }, clientOrderIdentifierList );
		}

		public async Task< OrderShipmentHistoryResponse[] > GetOrderShipmentHistoryListAsync( string[] clientOrderIdentifierList )
		{
			return await this.GetOrderShipmentHistoryListAsync( new int[] { }, clientOrderIdentifierList ).ConfigureAwait( false );
		}

		public OrderShipmentHistoryResponse[] GetOrderShipmentHistoryList( int[] orderIdList, string[] clientOrderIdentifierList )
		{
			return AP.CreateSubmit( this.AdditionalLogInfo ).Get( () =>
			{
				var result = this._client.GetOrderShipmentHistoryList( this._credentials, this.AccountId, orderIdList, clientOrderIdentifierList );
				this.CheckCaSuccess( result );
				return result.ResultData;
			} );
		}

		public async Task< OrderShipmentHistoryResponse[] > GetOrderShipmentHistoryListAsync( int[] orderIdList, string[] clientOrderIdentifierList )
		{
			return await AP.CreateSubmit( this.AdditionalLogInfo ).Get( async () =>
			{
				var result = await this._client.GetOrderShipmentHistoryListAsync( this._credentials, this.AccountId, orderIdList, clientOrderIdentifierList ).ConfigureAwait( false );
				this.CheckCaSuccess( result.GetOrderShipmentHistoryListResult );
				return result.GetOrderShipmentHistoryListResult.ResultData;
			} ).ConfigureAwait( false );
		}

		public ShippingCarrier[] GetShippingCarrierList()
		{
			var result = this._client.GetShippingCarrierList( this._credentials, this.AccountId );
			this.CheckCaSuccess( result );
			return result.ResultData;
		}

		public async Task< ShippingCarrier[] > GetShippingCarrierListAsync()
		{
			var result = await this._client.GetShippingCarrierListAsync( this._credentials, this.AccountId ).ConfigureAwait( false );
			this.CheckCaSuccess( result.GetShippingCarrierListResult );
			return result.GetShippingCarrierListResult.ResultData;
		}

		private void CheckCaSuccess( APIResultOfString results )
		{
			if( results.Status != ResultStatus.Success )
				throw new ChannelAdvisorException( results.MessageCode, results.Message );
		}

		private void CheckCaSuccess( APIResultOfArrayOfShipmentResponse result )
		{
			if( result.Status != ResultStatus.Success )
				throw new ChannelAdvisorException( result.MessageCode, result.Message );
			ChannelAdvisorException exceptionToThrow = null;
			foreach( var shipmentResponse in result.ResultData )
			{
				if( !shipmentResponse.Success )
				{
					string message = string.Format( "Error encountered while marking order shipped: {0}", shipmentResponse.Message );
					var callInfo = new CallInfoBasic( connectionInfo: this.ToJson(), additionalInfo: this.AdditionalLogInfo() );
					ChannelAdvisorLogger.LogTraceFailure( message, callInfo );
					if( exceptionToThrow == null )
						exceptionToThrow = new ChannelAdvisorException( shipmentResponse.Message );
				}
			}
			if( exceptionToThrow != null )
				throw exceptionToThrow;
		}

		private void CheckCaSuccess( APIResultOfArrayOfShippingCarrier result )
		{
			if( result.Status != ResultStatus.Success )
				throw new ChannelAdvisorException( result.MessageCode, result.Message );
		}

		private void CheckCaSuccess( APIResultOfArrayOfOrderShipmentHistoryResponse result )
		{
			if( result.Status != ResultStatus.Success )
				throw new ChannelAdvisorException( result.MessageCode, result.Message );
		}
	}
}