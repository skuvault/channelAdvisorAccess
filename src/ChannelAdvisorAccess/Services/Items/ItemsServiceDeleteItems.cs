using System;
using System.Threading.Tasks;
using ChannelAdvisorAccess.Exceptions;
using ChannelAdvisorAccess.Misc;

namespace ChannelAdvisorAccess.Services.Items
{
	public partial class ItemsService: IItemsService
	{
		#region Delete item
		public void DeleteItem( string sku, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );

				AP.CreateSubmit( this.AdditionalLogInfo, mark ).Do( () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
					var resultOfBoolean = this._client.DeleteInventoryItem( this._credentials, this.AccountId, sku );
					CheckCaSuccess( resultOfBoolean );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : resultOfBoolean.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
				} );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public async Task DeleteItemAsync( string sku, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );

				await AP.CreateSubmitAsync( this.AdditionalLogInfo, mark ).Do( async () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
					var resultOfBoolean = await this._client.DeleteInventoryItemAsync( this._credentials, this.AccountId, sku ).ConfigureAwait( false );
					CheckCaSuccess( resultOfBoolean.DeleteInventoryItemResult );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : resultOfBoolean.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
				} ).ConfigureAwait( false );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}
		#endregion}
	}
}