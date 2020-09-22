using System;
using System.Threading.Tasks;
using ChannelAdvisorAccess.Exceptions;
using ChannelAdvisorAccess.Misc;

namespace ChannelAdvisorAccess.Services.Items
{
	public partial class ItemsService: IItemsService
	{
		#region Ping
		public void Ping( Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );
				AP.CreateQuery( this.AdditionalLogInfo, mark ).Do( () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );
					var result = this._client.Ping( this._credentials );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : result.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );
					this.CheckCaSuccess( result );
				} );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public async Task PingAsync( Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );
				await AP.CreateQueryAsync( this.AdditionalLogInfo, mark ).Do( async () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );
					var result = await this._client.PingAsync( this._credentials ).ConfigureAwait( false );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : result.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );
					this.CheckCaSuccess( result.PingResult );
				} ).ConfigureAwait( false );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}
		#endregion
	}
}