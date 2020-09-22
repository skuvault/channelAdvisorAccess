using System;
using System.Threading.Tasks;
using ChannelAdvisorAccess.InventoryService;
using ChannelAdvisorAccess.Misc;

namespace ChannelAdvisorAccess.Services.Items
{
	public partial class ItemsService: IItemsService
	{
		#region Get Config Info
		public ClassificationConfigurationInformation[] GetClassificationConfigInfo( Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );

				var classificationConfigurationInformations = AP.CreateQuery( this.AdditionalLogInfo, mark ).Get( () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );
					var result = this._client.GetClassificationConfigurationInformation( this._credentials, this.AccountId );
					CheckCaSuccess( result );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : result.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );
					return result.ResultData;
				} );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : classificationConfigurationInformations.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );
				return classificationConfigurationInformations;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public async Task< ClassificationConfigurationInformation[] > GetClassificationConfigInfoAsync( Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );

				var classificationConfigurationInformations = await AP.CreateQueryAsync( this.AdditionalLogInfo, mark ).Get( async () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );
					var result = await this._client.GetClassificationConfigurationInformationAsync( this._credentials, this.AccountId ).ConfigureAwait( false );
					CheckCaSuccess( result.GetClassificationConfigurationInformationResult );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : result.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );
					return result.GetClassificationConfigurationInformationResult.ResultData;
				} ).ConfigureAwait( false );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : classificationConfigurationInformations.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );
				return classificationConfigurationInformations;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public DistributionCenterResponse[] GetDistributionCenterList( Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );

				var distributionCenterResponses = AP.CreateQuery( this.AdditionalLogInfo, mark ).Get( () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );
					var result = this._client.GetDistributionCenterList( this._credentials, this.AccountId );
					this.CheckCaSuccess( result );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : result.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );
					return result.ResultData;
				} );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : distributionCenterResponses.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );
				return distributionCenterResponses;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public async Task< DistributionCenterResponse[] > GetDistributionCenterListAsync( Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );

				var distributionCenterResponses = await AP.CreateQueryAsync( this.AdditionalLogInfo, mark ).Get( async () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );
					var result = await this._client.GetDistributionCenterListAsync( this._credentials, this.AccountId ).ConfigureAwait( false );
					this.CheckCaSuccess( result.GetDistributionCenterListResult );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : result.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );
					return result.GetDistributionCenterListResult.ResultData;
				} ).ConfigureAwait( false );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : distributionCenterResponses.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );
				return distributionCenterResponses;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}
		#endregion

		public Task< REST.Models.DistributionCenter[] > GetDistributionCentersAsync( Mark mark = null )
		{
			throw new NotImplementedException();
		}
	}
}