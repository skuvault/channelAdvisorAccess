using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ChannelAdvisorAccess.Exceptions;
using ChannelAdvisorAccess.Misc;

namespace ChannelAdvisorAccess.Services.Items
{
	public partial class ItemsService: IItemsService
	{
		#region  Skus
		public IEnumerable< string > GetAllSkus( Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );
				var filteredSkus = this.GetFilteredSkus( new ItemsFilter(), mark );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : filteredSkus.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );
				return filteredSkus;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public async Task< IEnumerable< string > > GetAllSkusAsync( Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );
				var filteredSkus = await this.GetFilteredSkusAsync( new ItemsFilter(), mark ).ConfigureAwait( false );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : filteredSkus.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );
				return filteredSkus;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public IEnumerable< string > GetFilteredSkus( ItemsFilter filter, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : filter.ToJson() ) );

				filter.Criteria.PageSize = 100;
				filter.Criteria.PageNumber = 0;

				filter.DetailLevel.IncludeClassificationInfo = true;
				filter.DetailLevel.IncludePriceInfo = true;
				filter.DetailLevel.IncludeQuantityInfo = true;

				var filteredSkus = new List< string >();
				while( true )
				{
					filter.Criteria.PageNumber += 1;
					var itemResponse = AP.CreateQuery( this.AdditionalLogInfo, mark ).Get(
						() =>
						{
							ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : filter.ToJson() ) );
							var apiResultOfArrayOfString = this._client.GetFilteredSkuList
								(
									this._credentials, this.AccountId, filter.Criteria,
									filter.SortField, filter.SortDirection );
							ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : apiResultOfArrayOfString.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : filter.ToJson() ) );

							return apiResultOfArrayOfString;
						} );
					ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : itemResponse.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );

					if( !this.IsRequestSuccessful( itemResponse, mark: mark ) )
					{
						filteredSkus.Add( null );
						continue;
					}

					var items = itemResponse.ResultData;

					if( items == null )
					{
						ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : filteredSkus.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : filter.ToJson() ) );
						return filteredSkus;
					}

					filteredSkus.AddRange( items );

					if( items.Length == 0 || items.Length < filter.Criteria.PageSize )
					{
						ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : filteredSkus.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : filter.ToJson() ) );
						return filteredSkus;
					}
				}
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public async Task< IEnumerable< string > > GetFilteredSkusAsync( ItemsFilter filter, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : filter.ToJson() ) );
				filter.Criteria.PageSize = 100;
				filter.Criteria.PageNumber = 0;

				var skus = new List< string >();
				while( true )
				{
					filter.Criteria.PageNumber += 1;
					var itemResponse = await AP.CreateQueryAsync( this.AdditionalLogInfo, mark ).Get( async () =>
					{
						ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : filter.ToJson() ) );
						var getFilteredSkuListResponse = await this._client.GetFilteredSkuListAsync
							( this._credentials, this.AccountId, filter.Criteria,
								filter.SortField, filter.SortDirection ).ConfigureAwait( false );
						ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : getFilteredSkuListResponse.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : filter.ToJson() ) );
						return getFilteredSkuListResponse;
					} ).ConfigureAwait( false );

					ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : itemResponse.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );

					if( !this.IsRequestSuccessful( itemResponse.GetFilteredSkuListResult, mark: mark ) )
						continue;

					var pageSkus = itemResponse.GetFilteredSkuListResult.ResultData;
					if( pageSkus == null )
					{
						ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : skus.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : filter.ToJson() ) );
						return skus;
					}

					skus.AddRange( pageSkus );

					if( pageSkus.Length == 0 || pageSkus.Length < filter.Criteria.PageSize )
					{
						ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : skus.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : filter.ToJson() ) );
						return skus;
					}
				}
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public async Task< PagedApiResponse< string > > GetFilteredSkusAsync( ItemsFilter filter, int startPage, int pageLimit, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			var parameters = new { filter, startPage, pageLimit };

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : parameters.ToJson() ) );

				filter.Criteria.PageSize = 100;
				filter.Criteria.PageNumber = ( startPage > 0 ) ? startPage - 1 : 1;

				var skus = new List< string >();
				for( var iteration = 0; iteration < pageLimit; iteration++ )
				{
					filter.Criteria.PageNumber += 1;

					var itemResponse = await AP.CreateQueryAsync( this.AdditionalLogInfo, mark ).Get( async () =>
					{
						ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : parameters.ToJson() ) );
						var getFilteredSkuListResponse = await this._client.GetFilteredSkuListAsync( this._credentials, this.AccountId, filter.Criteria, filter.SortField, filter.SortDirection )
							.ConfigureAwait( false );
						ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : getFilteredSkuListResponse.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : parameters.ToJson() ) );
						return getFilteredSkuListResponse;
					} ).ConfigureAwait( false );

					ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : itemResponse.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : parameters.ToJson() ) );

					if( !this.IsRequestSuccessful( itemResponse.GetFilteredSkuListResult, mark: mark ) )
						continue;

					var pageSkus = itemResponse.GetFilteredSkuListResult.ResultData;
					if( pageSkus == null )
					{
						var pagedApiResponse = new PagedApiResponse< string >( skus, filter.Criteria.PageNumber, true );
						ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : pagedApiResponse.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : parameters.ToJson() ) );
						return pagedApiResponse;
					}

					skus.AddRange( pageSkus );

					if( pageSkus.Length == 0 || pageSkus.Length < filter.Criteria.PageSize )
					{
						var pagedApiResponse = new PagedApiResponse< string >( skus, filter.Criteria.PageNumber, true );
						ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : pagedApiResponse.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : parameters.ToJson() ) );
						return pagedApiResponse;
					}
				}

				var apiResponse = new PagedApiResponse< string >( skus, filter.Criteria.PageNumber, false );
				ChannelAdvisorLogger.LogTraceEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : apiResponse.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : parameters.ToJson() ) );
				return apiResponse;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}
		#endregion
	}
}