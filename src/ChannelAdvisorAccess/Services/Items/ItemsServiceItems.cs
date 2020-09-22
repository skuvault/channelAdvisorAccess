using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Threading.Tasks;
using ChannelAdvisorAccess.InventoryService;
using ChannelAdvisorAccess.Misc;
using Netco.Extensions;

namespace ChannelAdvisorAccess.Services.Items
{
	public partial class ItemsService
	{
		#region Get items
		public bool DoesSkuExist( string sku, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : sku.ToJson() ) );
				var skuExist = AP.CreateQuery( this.AdditionalLogInfo, mark ).Get( () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku.ToJson() ) );
					var result = this._client.DoesSkuExist( this._credentials, this.AccountId, sku );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : result.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku.ToJson() ) );
					return result;
				} );

				ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : skuExist.ToJson(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : sku.ToJson() ) );
				var resultWithSuccessCheck = this.GetResultWithSuccessCheck( skuExist, skuExist.ResultData );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodResult : resultWithSuccessCheck.ToJson(), methodParameters : sku.ToJson() ) );
				return resultWithSuccessCheck;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public async Task< bool > DoesSkuExistAsync( string sku, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : sku.ToJson() ) );
				var skuExist = await AP.CreateQueryAsync( this.AdditionalLogInfo, mark ).Get( async () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark: mark, additionalInfo: this.AdditionalLogInfo(), methodParameters: !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku.ToJson() ) );
					var result = await this._client.DoesSkuExistAsync( this._credentials, this.AccountId, sku ).ConfigureAwait( false );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark: mark, methodResult: !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : result.ToJson(), additionalInfo: this.AdditionalLogInfo(), methodParameters: !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku.ToJson() ) );
					return result;
				} ).ConfigureAwait( false );
				ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : skuExist.ToJson(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : sku.ToJson() ) );
				var resultWithSuccessCheck = this.GetResultWithSuccessCheck( skuExist.DoesSkuExistResult, skuExist.DoesSkuExistResult.ResultData );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodResult : resultWithSuccessCheck.ToJson(), methodParameters : sku.ToJson() ) );
				return resultWithSuccessCheck;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public IEnumerable< DoesSkuExistResponse > DoSkusExist( IEnumerable< string > skus, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : skus.ToJson() ) );

				var skusParts = ToChunks( skus, 500 );
				var responses = new List< DoesSkuExistResponse >();
				foreach( var skusPage in skusParts )
				{
					var doesSkuExistResponses = AP.CreateQuery( this.AdditionalLogInfo, mark ).Get( () => 
					{
						ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : skusPage.ToJson() ) );
						var skusResult = this._client.DoesSkuExistList( this._credentials, this.AccountId, skusPage.ToArray() );
						var resultWithSuccessCheck = this.GetResultWithSuccessCheck( skusResult, skusResult.ResultData, false );
						ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : resultWithSuccessCheck.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : skusPage.ToJson() ) );
						return resultWithSuccessCheck;
					} );

					if( doesSkuExistResponses != null )
						responses.AddRange( doesSkuExistResponses.ToList() );
				}
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodResult : responses.ToJson(), methodParameters : skus.ToJson() ) );
				return responses;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public async Task< IEnumerable< DoesSkuExistResponse > > DoSkusExistAsync( IEnumerable< string > skus, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : skus.ToJson() ) );

				var skusParts = ToChunks( skus, 500 );
				var doesSkuExistResponses = ( await skusParts.ProcessInBatchAsync( 3, async skusPage =>
				{
					var doesSkuExistResponsesByPage = await AP.CreateQueryAsync( this.AdditionalLogInfo, mark ).Get( async () =>
					{
						ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : skus.ToJson() ) );
						var skusResult = await this._client.DoesSkuExistListAsync( this._credentials, this.AccountId, skusPage.ToArray() ).ConfigureAwait( false );
						var resultWithSuccessCheck = this.GetResultWithSuccessCheck( skusResult.DoesSkuExistListResult, skusResult.DoesSkuExistListResult.ResultData );
						ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : resultWithSuccessCheck.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : skus.ToJson() ) );
						return resultWithSuccessCheck;
					} ).ConfigureAwait( false );
					
					return doesSkuExistResponsesByPage;
				} ).ConfigureAwait( false ) ).SelectMany( x => x );
				
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodResult : doesSkuExistResponses.ToJson(), methodParameters : skus.ToJson() ) );
				return doesSkuExistResponses;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public IEnumerable< InventoryItemResponse > GetAllItems( Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );

				IEnumerable< InventoryItemResponse > inventoryItemResponses;
				if( this.UseCache() )
					inventoryItemResponses = this.GetCachedInventory();
				else
					inventoryItemResponses = this.DownloadAllItems();
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodResult : inventoryItemResponses.ToJson() ) );

				return inventoryItemResponses;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		private IEnumerable< InventoryItemResponse > GetCachedInventory( Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			ChannelAdvisorLogger.LogTraceStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );
			IEnumerable< InventoryItemResponse > inventoryItemResponses;
			lock( this._inventoryCacheLock )
			{
				var cachedInventory = this._cache.Get( this._allItemsCacheKey ) as IEnumerable< InventoryItemResponse >;
				if( cachedInventory != null )
					inventoryItemResponses = cachedInventory;
				else
				{
					var items = this.DownloadAllItems( mark ).ToList();
					this._cache.Set( this._allItemsCacheKey, items, new CacheItemPolicy { SlidingExpiration = this.SlidingCacheExpiration } );
					inventoryItemResponses = items;
				}
			}
			ChannelAdvisorLogger.LogTraceEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodResult : inventoryItemResponses.ToJson() ) );
			return inventoryItemResponses;
		}

		private IEnumerable< InventoryItemResponse > DownloadAllItems( Mark mark = null )
		{
			ChannelAdvisorLogger.LogTraceStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );

			var filter = new ItemsFilter
			{
				DetailLevel = { IncludeClassificationInfo = true, IncludePriceInfo = true, IncludeQuantityInfo = true }
			};

			var inventoryItemResponses = this.GetFilteredItems( filter, mark );
			ChannelAdvisorLogger.LogTraceEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodResult : inventoryItemResponses.ToJson() ) );

			return inventoryItemResponses;
		}

		private bool UseCache()
		{
			return this._cache != null;
		}

		/// <summary>
		/// Gets the items by skus.
		/// </summary>
		/// <param name="skus">The skus.</param>
		/// <param name="mark">use it to simplify navigation inside log, allows to view call ierarchy</param>
		/// <returns>Enumerator to process items. <c>null</c> is returned
		/// for non-existing skus.</returns>
		/// <remarks>Items are pulled 1 at a time to handle non-existing skus.
		/// This results in slower performance.</remarks>
		public IEnumerable< InventoryItemResponse > GetItems( IEnumerable< string > skus, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );
				var checkedSkus = this.DoSkusExist( skus, mark );
				var existingSkus = checkedSkus.Where( s => s.Result ).Select( s => s.Sku );

				var message = "{\"ExistingSkus\":\"" + existingSkus.ToJson() + "\"}";
				ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), notes : message ) );

				var inventoryItemResponses = existingSkus.ProcessWithPages( 100, skusPage =>
				{
					var itemsResult = AP.CreateQuery( this.AdditionalLogInfo, mark ).Get( () =>
					{
						ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : skusPage.ToJson() ) );
						var apiResultOfArrayOfInventoryItemResponse = this._client.GetInventoryItemList( this._credentials, this.AccountId, skusPage.ToArray() );
						ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : apiResultOfArrayOfInventoryItemResponse.ToJson(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : skusPage.ToJson() ) );
						return apiResultOfArrayOfInventoryItemResponse;
					} );
					return this.GetResultWithSuccessCheck( itemsResult, itemsResult.ResultData );
				} );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : skus.ToJson(), methodResult : inventoryItemResponses.ToJson() ) );
				return inventoryItemResponses;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public async Task< IEnumerable< InventoryItemResponse > > GetItemsAsync( IEnumerable< string > skus, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );
				var checkedSkus = await this.DoSkusExistAsync( skus, mark ).ConfigureAwait( false );
				var existingSkus = checkedSkus.Where( s => s.Result ).Select( s => s.Sku );

				var message = "{\"ExistingSkus\":\"" + existingSkus.ToJson() + "\"}";
				ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), notes : message ) );

				var inventoryItemResponses = await existingSkus.ProcessWithPagesAsync< string, InventoryItemResponse >( 100, async skusPage =>
				{
					var itemsResult = await AP.CreateQueryAsync( this.AdditionalLogInfo, mark ).Get( async () =>
					{
						ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : skusPage.ToJson() ) );
						var getInventoryItemListResponse = await this._client.GetInventoryItemListAsync( this._credentials, this.AccountId, skusPage.ToArray() ).ConfigureAwait( false );
						ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : getInventoryItemListResponse.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : skusPage.ToJson() ) );
						return getInventoryItemListResponse;
					} ).ConfigureAwait( false );

					ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : skus.ToJson(), methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : itemsResult.ToJson() ) );

					return this.GetResultWithSuccessCheck( itemsResult.GetInventoryItemListResult, itemsResult.GetInventoryItemListResult.ResultData );
				} ).ConfigureAwait( false );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : skus.ToJson(), methodResult : inventoryItemResponses.ToJson() ) );
				return inventoryItemResponses;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		/// <summary>
		/// Gets the items matching filter.
		/// </summary>
		/// <param name="filter">The filter.</param>
		/// <param name="mark">use it to simplify navigation inside log, allows to view call ierarchy</param>
		/// <returns>Items matching supplied filter.</returns>
		/// <seealso href="http://developer.channeladvisor.com/display/cadn/GetFilteredInventoryItemList"/>
		public IEnumerable< InventoryItemResponse > GetFilteredItems( ItemsFilter filter, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : filter.ToJson() ) );
				filter.Criteria.PageSize = 100;
				filter.Criteria.PageNumber = 0;

				var filteredItems = new List< InventoryItemResponse >();
				while( true )
				{
					filter.Criteria.PageNumber += 1;
					var itemResponse = AP.CreateQuery( this.AdditionalLogInfo, mark ).Get( () =>
					{
						ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : filter.ToJson() ) );
						var apiResultOfArrayOfInventoryItemResponse = this._client.GetFilteredInventoryItemList
							(
								this._credentials,
								this.AccountId, filter.Criteria, filter.DetailLevel,
								filter.SortField, filter.SortDirection );
						ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : apiResultOfArrayOfInventoryItemResponse.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : filter.ToJson() ) );
						return apiResultOfArrayOfInventoryItemResponse;
					} );

					if( !this.IsRequestSuccessful( itemResponse, mark: mark ) )
					{
						filteredItems.Add( null );
						continue;
					}

					var items = itemResponse.ResultData;

					if( items == null )
					{
						ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodResult : filteredItems.ToJson(), methodParameters : filter.ToJson() ) );
						return filteredItems;
					}

					filteredItems.AddRange( items );

					if( items.Length == 0 || items.Length < filter.Criteria.PageSize )
					{
						ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodResult : filteredItems.ToJson(), methodParameters : filter.ToJson() ) );
						return filteredItems;
					}
				}
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		/// <summary>
		/// Gets the items matching filter.
		/// </summary>
		/// <param name="filter">The filter.</param>
		/// <param name="mark">use it to simplify navigation inside log, allows to view call ierarchy</param>
		/// <returns>Items matching supplied filter.</returns>
		/// <seealso href="http://developer.channeladvisor.com/display/cadn/GetFilteredInventoryItemList"/>
		public async Task< IEnumerable< InventoryItemResponse > > GetFilteredItemsAsync( ItemsFilter filter, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : filter.ToJson() ) );
				filter.Criteria.PageSize = 100;
				filter.Criteria.PageNumber = 0;

				var items = new List< InventoryItemResponse >();
				while( true )
				{
					filter.Criteria.PageNumber += 1;
					var itemResponse = await AP.CreateQueryAsync( this.AdditionalLogInfo, mark ).Get( async () =>
					{
						ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : filter.ToJson() ) );
						var getFilteredInventoryItemListResponse = await this._client.GetFilteredInventoryItemListAsync
							( this._credentials,
								this.AccountId, filter.Criteria, filter.DetailLevel,
								filter.SortField, filter.SortDirection ).ConfigureAwait( false );
						ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : getFilteredInventoryItemListResponse.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : filter.ToJson() ) );
						return getFilteredInventoryItemListResponse;
					}
						).ConfigureAwait( false );

					if( !this.IsRequestSuccessful( itemResponse.GetFilteredInventoryItemListResult, mark: mark ) )
						continue;

					var pageItems = itemResponse.GetFilteredInventoryItemListResult.ResultData;
					if( pageItems == null )
					{
						ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : items.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : filter.ToJson() ) );
						return items;
					}

					items.AddRange( pageItems );

					if( pageItems.Length == 0 || pageItems.Length < filter.Criteria.PageSize )
					{
						ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : items.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : filter.ToJson() ) );
						return items;
					}
				}
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		/// <summary>
		/// Gets the items matching filter.
		/// </summary>
		/// <param name="filter">The filter.</param>
		/// <param name="startPage">The first page number to query.</param>
		/// <param name="pageLimit">The max number of pages to query.</param>
		/// <param name="mark">use it to simplify navigation inside log, allows to view call ierarchy</param>
		/// <returns>Items matching supplied filter.</returns>
		/// <seealso href="http://developer.channeladvisor.com/display/cadn/GetFilteredInventoryItemList"/>
		public async Task< PagedApiResponse< InventoryItemResponse > > GetFilteredItemsAsync( ItemsFilter filter, int startPage, int pageLimit, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : filter.ToJson() ) );
				filter.Criteria.PageSize = 100;
				filter.Criteria.PageNumber = ( startPage > 0 ) ? startPage - 1 : 1;

				var items = new List< InventoryItemResponse >();
				for( var iteration = 0; iteration < pageLimit; iteration++ )
				{
					filter.Criteria.PageNumber += 1;

					var itemResponse = await AP.CreateQueryAsync( this.AdditionalLogInfo, mark ).Get( async () =>
					{
						ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : filter.ToJson() ) );
						var getFilteredInventoryItemListResponse = await this._client.GetFilteredInventoryItemListAsync( this._credentials, this.AccountId, filter.Criteria, filter.DetailLevel, filter.SortField, filter.SortDirection )
							.ConfigureAwait( false );
						ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : getFilteredInventoryItemListResponse.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : filter.ToJson() ) );
						return getFilteredInventoryItemListResponse;
					} ).ConfigureAwait( false );

					if( !this.IsRequestSuccessful( itemResponse.GetFilteredInventoryItemListResult, mark: mark ) )
						continue;

					var pageItems = itemResponse.GetFilteredInventoryItemListResult.ResultData;
					if( pageItems == null )
					{
						var pagedApiResponse = new PagedApiResponse< InventoryItemResponse >( items, filter.Criteria.PageNumber, true );
						ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, notes : "PageResponse", methodResult : pagedApiResponse.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : filter.ToJson() ) );
						return pagedApiResponse;
					}

					items.AddRange( pageItems );
					if( pageItems.Length == 0 || pageItems.Length < filter.Criteria.PageSize )
					{
						var pagedApiResponse = new PagedApiResponse< InventoryItemResponse >( items, filter.Criteria.PageNumber, true );
						ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, notes : "PageResponse", methodResult : pagedApiResponse.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : filter.ToJson() ) );
						return pagedApiResponse;
					}
				}

				var apiResponse = new PagedApiResponse< InventoryItemResponse >( items, filter.Criteria.PageNumber, false );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : apiResponse.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : filter.ToJson() ) );
				return apiResponse;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public AttributeInfo[] GetAttributes( string sku, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
				var attributeList = AP.CreateQuery( this.AdditionalLogInfo, mark ).Get( () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
					var apiResultOfArrayOfAttributeInfo = this._client.GetInventoryItemAttributeList( this._credentials, this.AccountId, sku );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : apiResultOfArrayOfAttributeInfo.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
					return apiResultOfArrayOfAttributeInfo;
				}
					);
				ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : attributeList.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : sku ) );
				var resultWithSuccessCheck = this.GetResultWithSuccessCheck( attributeList, attributeList.ResultData );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : resultWithSuccessCheck.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
				return resultWithSuccessCheck;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public async Task< AttributeInfo[] > GetAttributesAsync( string sku, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
				var attributeList = await AP.CreateQueryAsync( this.AdditionalLogInfo, mark ).Get( async () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
					var getInventoryItemAttributeListResponse = await this._client.GetInventoryItemAttributeListAsync( this._credentials, this.AccountId, sku ).ConfigureAwait( false );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : getInventoryItemAttributeListResponse.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
					return getInventoryItemAttributeListResponse;
				} ).ConfigureAwait( false );

				ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : attributeList.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : sku ) );
				var attributeInfos = !this.IsRequestSuccessfulAttribute( attributeList ) ? default( AttributeInfo[] ) : attributeList.GetInventoryItemAttributeListResult.ResultData;
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : attributeInfos.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
				return attributeInfos;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		/// <summary>
		/// Gets the additional item quantities.
		/// </summary>
		/// <param name="sku">The sku.</param>
		/// <param name="mark"></param>
		/// <returns>Item quantities.</returns>
		/// <see href="http://developer.channeladvisor.com/display/cadn/GetInventoryItemQuantityInfo"/>
		public QuantityInfoResponse GetItemQuantities( string sku, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
				var requestResult = AP.CreateQuery( this.AdditionalLogInfo, mark ).Get( () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
					var apiResultOfQuantityInfoResponse = this._client.GetInventoryItemQuantityInfo( this._credentials, this.AccountId, sku );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : apiResultOfQuantityInfoResponse.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );

					return apiResultOfQuantityInfoResponse;
				}
					);
				ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : requestResult.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : sku ) );
				var resultWithSuccessCheck = this.GetResultWithSuccessCheck( requestResult, requestResult.ResultData );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : resultWithSuccessCheck.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );

				return resultWithSuccessCheck;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public async Task< QuantityInfoResponse > GetItemQuantitiesAsync( string sku, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
				var requestResult = await AP.CreateQueryAsync( this.AdditionalLogInfo, mark ).Get( async () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
					var getInventoryItemQuantityInfoResponse = await this._client.GetInventoryItemQuantityInfoAsync( this._credentials, this.AccountId, sku ).ConfigureAwait( false );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : getInventoryItemQuantityInfoResponse.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );

					return getInventoryItemQuantityInfoResponse;
				} ).ConfigureAwait( false );

				ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : requestResult.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : sku ) );
				var resultWithSuccessCheck = this.GetResultWithSuccessCheck( requestResult.GetInventoryItemQuantityInfoResult, requestResult.GetInventoryItemQuantityInfoResult.ResultData );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : resultWithSuccessCheck.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );

				return resultWithSuccessCheck;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public ClassificationConfigurationInformation[] GetClassificationConfigurationInformation( Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );
				var requestResult = AP.CreateQuery( this.AdditionalLogInfo, mark ).Get( () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );
					var apiResultOfArrayOfClassificationConfigurationInformation = this._client.GetClassificationConfigurationInformation( this._credentials, this.AccountId );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : apiResultOfArrayOfClassificationConfigurationInformation.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );

					return apiResultOfArrayOfClassificationConfigurationInformation;
				} );
				ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : requestResult.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );
				var resultWithSuccessCheck = this.GetResultWithSuccessCheck( requestResult, requestResult.ResultData );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : resultWithSuccessCheck.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );

				return resultWithSuccessCheck;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public async Task< ClassificationConfigurationInformation[] > GetClassificationConfigurationInformationAsync( Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );
				var requestResult = await AP.CreateQueryAsync( this.AdditionalLogInfo, mark ).Get( async () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );
					var getClassificationConfigurationInformationResponse = await this._client.GetClassificationConfigurationInformationAsync( this._credentials, this.AccountId ).ConfigureAwait( false );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : getClassificationConfigurationInformationResponse.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );
					return getClassificationConfigurationInformationResponse;
				} ).ConfigureAwait( false );
				ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : requestResult.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );
				var resultWithSuccessCheck = this.GetResultWithSuccessCheck( requestResult.GetClassificationConfigurationInformationResult, requestResult.GetClassificationConfigurationInformationResult.ResultData );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : resultWithSuccessCheck.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );

				return resultWithSuccessCheck;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public StoreInfo GetStoreInfo( string sku, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );

				var requestResult = AP.CreateQuery( this.AdditionalLogInfo, mark ).Get( () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
					var apiResultOfStoreInfo = this._client.GetInventoryItemStoreInfo( this._credentials, this.AccountId, sku );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : apiResultOfStoreInfo.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
					return apiResultOfStoreInfo;
				} );
				ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : requestResult.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );
				var resultWithSuccessCheck = this.GetResultWithSuccessCheck( requestResult, requestResult.ResultData );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : resultWithSuccessCheck.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
				return resultWithSuccessCheck;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public async Task< StoreInfo > GetStoreInfoAsync( string sku, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
				var requestResult = await AP.CreateQueryAsync( this.AdditionalLogInfo, mark ).Get( async () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
					var getInventoryItemStoreInfoResponse = await this._client.GetInventoryItemStoreInfoAsync( this._credentials, this.AccountId, sku ).ConfigureAwait( false );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : getInventoryItemStoreInfoResponse.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
					return getInventoryItemStoreInfoResponse;
				} ).ConfigureAwait( false );
				ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : requestResult.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : sku ) );
				var resultWithSuccessCheck = this.GetResultWithSuccessCheck( requestResult.GetInventoryItemStoreInfoResult, requestResult.GetInventoryItemStoreInfoResult.ResultData );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : resultWithSuccessCheck.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
				return resultWithSuccessCheck;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public ImageInfoResponse[] GetImageList( string sku, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
				var requestResult = AP.CreateQuery( this.AdditionalLogInfo, mark ).Get( () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
					var apiResultOfArrayOfImageInfoResponse = this._client.GetInventoryItemImageList( this._credentials, this.AccountId, sku );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : apiResultOfArrayOfImageInfoResponse.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
					return apiResultOfArrayOfImageInfoResponse;
				} );
				ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : requestResult.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );
				var resultWithSuccessCheck = this.GetResultWithSuccessCheck( requestResult, requestResult.ResultData );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : resultWithSuccessCheck.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
				return resultWithSuccessCheck;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public async Task< ImageInfoResponse[] > GetImageListAsync( string sku, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
				var requestResult = await AP.CreateQueryAsync( this.AdditionalLogInfo, mark ).Get( async () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
					var getInventoryItemImageListResponse = await this._client.GetInventoryItemImageListAsync( this._credentials, this.AccountId, sku ).ConfigureAwait( false );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : getInventoryItemImageListResponse.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
					return getInventoryItemImageListResponse;
				} ).ConfigureAwait( false );

				ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : requestResult.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );
				var imageInfoResponses = !this.IsRequestSuccessfulImage( requestResult ) ? default( ImageInfoResponse[] ) : requestResult.GetInventoryItemImageListResult.ResultData;
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : imageInfoResponses.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
				return imageInfoResponses;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public DistributionCenterInfoResponse[] GetShippingInfo( string sku, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
				var requestResult = AP.CreateQuery( this.AdditionalLogInfo, mark ).Get( () =>
				{
					ChannelAdvisorLogger.LogTraceStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
					var apiResultOfArrayOfDistributionCenterInfoResponse = this._client.GetInventoryItemShippingInfo( this._credentials, this.AccountId, sku );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : apiResultOfArrayOfDistributionCenterInfoResponse.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
					return apiResultOfArrayOfDistributionCenterInfoResponse;
				} );
				ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : requestResult.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );
				var distributionCenterInfoResponses = this.GetResultWithSuccessCheck( requestResult, requestResult.ResultData );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : distributionCenterInfoResponses.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
				return distributionCenterInfoResponses;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public async Task< DistributionCenterInfoResponse[] > GetShippingInfoAsync( string sku, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
				var requestResult = await AP.CreateQueryAsync( this.AdditionalLogInfo, mark ).Get( async () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
					var getInventoryItemShippingInfoResponse = await this._client.GetInventoryItemShippingInfoAsync( this._credentials, this.AccountId, sku ).ConfigureAwait( false );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : getInventoryItemShippingInfoResponse.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
					return getInventoryItemShippingInfoResponse;
				} ).ConfigureAwait( false );

				ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : requestResult.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );
				var distributionCenterInfoResponses = this.GetResultWithSuccessCheck( requestResult.GetInventoryItemShippingInfoResult, requestResult.GetInventoryItemShippingInfoResult.ResultData );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : distributionCenterInfoResponses.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
				return distributionCenterInfoResponses;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public VariationInfo GetVariationInfo( string sku, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
				var requestResult = AP.CreateQuery( this.AdditionalLogInfo, mark ).Get( () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
					var apiResultOfVariationInfo = this._client.GetInventoryItemVariationInfo( this._credentials, this.AccountId, sku );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : apiResultOfVariationInfo.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );

					return apiResultOfVariationInfo;
				} );
				ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : requestResult.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );
				var resultWithSuccessCheck = this.GetResultWithSuccessCheck( requestResult, requestResult.ResultData );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : resultWithSuccessCheck.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
				return resultWithSuccessCheck;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public async Task< VariationInfo > GetVariationInfoAsync( string sku, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
				var requestResult = await AP.CreateQueryAsync( this.AdditionalLogInfo, mark ).Get( async () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
					var getInventoryItemVariationInfoResponse = await this._client.GetInventoryItemVariationInfoAsync( this._credentials, this.AccountId, sku ).ConfigureAwait( false );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : getInventoryItemVariationInfoResponse.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
					return getInventoryItemVariationInfoResponse;
				} ).ConfigureAwait( false );
				ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : requestResult.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );
				var resultWithSuccessCheck = this.GetResultWithSuccessCheck( requestResult.GetInventoryItemVariationInfoResult, requestResult.GetInventoryItemVariationInfoResult.ResultData );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : resultWithSuccessCheck.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
				return resultWithSuccessCheck;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		/// <summary>
		/// Gets the available quantity.
		/// </summary>
		/// <param name="sku">The sku of the item.</param>
		/// <param name="mark"></param>
		/// <returns>
		/// The Available quantity for the specified sku.
		/// </returns>
		/// <see href="http://developer.channeladvisor.com/display/cadn/GetInventoryQuantity"/>
		public int GetAvailableQuantity( string sku, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
				var quantityResult = AP.CreateQuery( this.AdditionalLogInfo, mark ).Get( () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
					var result = this._client.GetInventoryQuantity( this._credentials, this.AccountId, sku );
					CheckCaSuccess( result );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : result.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
					return result;
				} );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : quantityResult.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
				return quantityResult.ResultData;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public async Task< int > GetAvailableQuantityAsync( string sku, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
				var quantityResult = await AP.CreateQueryAsync( this.AdditionalLogInfo, mark ).Get( async () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
					var result = await this._client.GetInventoryQuantityAsync( this._credentials, this.AccountId, sku ).ConfigureAwait( false );
					CheckCaSuccess( result.GetInventoryQuantityResult );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : result.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : sku ) );
					return result.GetInventoryQuantityResult;
				} ).ConfigureAwait( false );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : quantityResult.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : sku ) );
				return quantityResult.ResultData;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		internal static IEnumerable< List< T > > ToChunks< T >( IEnumerable< T > items, int chunkSize )
		{
			var chunk = new List< T >( chunkSize );
			foreach( var item in items )
			{
				chunk.Add( item );
				if( chunk.Count == chunkSize )
				{
					yield return chunk;
					chunk = new List< T >( chunkSize );
				}
			}
			if( chunk.Any() )
				yield return chunk;
		}

		public IEnumerable< InventoryQuantityResponse > GetAvailableQuantities( IEnumerable< string > skus, Mark mark = null, int delayInMs = 5000 )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : skus.ToJson() ) );

				var skusParts = ToChunks( skus, 100 );
				var inventoryQuantityResponses = new List< InventoryQuantityResponse >();

				foreach( var s in skusParts )
				{
					var requestResult = AP.CreateQuery( this.AdditionalLogInfo, mark ).Get( () =>
					{
						ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : skus.ToJson() ) );
						var apiResultOfArrayOfInventoryQuantityResponse = this._client.GetInventoryQuantityList( this._credentials, this.AccountId, s.ToArray() );
						ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : apiResultOfArrayOfInventoryQuantityResponse.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : skus.ToJson() ) );
						return apiResultOfArrayOfInventoryQuantityResponse;
					} );

					var resultWithSuccessCheck = this.GetResultWithSuccessCheck( requestResult, requestResult.ResultData );
					ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : s.ToJson(), methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : resultWithSuccessCheck.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );
					inventoryQuantityResponses.AddRange( resultWithSuccessCheck );
				}

				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : inventoryQuantityResponses.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : skus.ToJson() ) );
				return inventoryQuantityResponses;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public async Task< IEnumerable< InventoryQuantityResponse > > GetAvailableQuantitiesAsync( IEnumerable< string > skus, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : skus.ToJson() ) );

				var skusParts = ToChunks( skus, 100 );
				var inventoryQuantityResponses = ( await skusParts.ProcessInBatchAsync( 3, async s =>
				{
					var requestResult = await AP.CreateQueryAsync( this.AdditionalLogInfo, mark ).Get( async () =>
					{
						ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : skus.ToJson() ) );
						var apiResultOfArrayOfInventoryQuantityResponse = await this._client.GetInventoryQuantityListAsync( this._credentials, this.AccountId, s.ToArray() ).ConfigureAwait( false );
						ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : apiResultOfArrayOfInventoryQuantityResponse.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : skus.ToJson() ) );
						return apiResultOfArrayOfInventoryQuantityResponse;
					} ).ConfigureAwait( false );

					var resultWithSuccessCheck = this.GetResultWithSuccessCheck( requestResult.GetInventoryQuantityListResult, requestResult.GetInventoryQuantityListResult.ResultData );
					ChannelAdvisorLogger.LogTrace( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : s.ToJson(), methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndReturnsForTrace ) ? null : resultWithSuccessCheck.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );
					return resultWithSuccessCheck;
				} ).ConfigureAwait( false ) ).SelectMany( x => x );

				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : inventoryQuantityResponses.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : skus.ToJson() ) );
				return inventoryQuantityResponses;
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}
		#endregion}
	}
}