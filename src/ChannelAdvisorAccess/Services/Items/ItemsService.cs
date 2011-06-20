using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using ChannelAdvisorAccess.Exceptions;
using ChannelAdvisorAccess.InventoryService;
using ChannelAdvisorAccess.Misc;
using Netco.Profiling;

namespace ChannelAdvisorAccess.Services.Items
{
	public class ItemsService : IItemsService
	{
		private readonly APICredentials _credentials;
		private readonly InventoryServiceSoapClient _client;

		private readonly ObjectCache _cache;
		private readonly string _allItemsCacheKey;
		private readonly object _inventoryCacheLock = new Object();
		
		public string Name { get; private set; }
		public string AccountId{ get; private set; }
		public TimeSpan SlidingCacheExpiration { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ItemsService"/> class.
		/// </summary>
		/// <param name="credentials">The credentials.</param>
		/// <param name="name">The account user-friendly name.</param>
		/// <param name="accountId">The account id.</param>
		/// <param name="cache">The cache.</param>
		/// <remarks>If <paramref name="cache"/> is <c>null</c> no caching takes place.</remarks>
		public ItemsService( APICredentials credentials, string name, string accountId, ObjectCache cache = null )
		{
			this._credentials = credentials;
			this.AccountId = accountId;
			this._client = new InventoryServiceSoapClient();

			this.Name = name;
			this._cache = cache;
			this.SlidingCacheExpiration = ObjectCache.NoSlidingExpiration;
			this._allItemsCacheKey = "caAllItems_ID_{0}".FormatWith( this.AccountId );
		}
		
		#region Get items
		public bool DoesSkuExist( string sku )
		{
			var skuExist = ActionPolicies.CaGetPolicy.Get( () => this._client.DoesSkuExist( this._credentials, this.AccountId, sku ) );
			return GetResultWithSuccessCheck( skuExist, skuExist.ResultData );
		}

		public IEnumerable< InventoryItemResponse > GetAllItems()
		{
			if( UseCache() )
				return GetCachedInventory();
			else
				return DownloadAllItems();
		}

		private IEnumerable< InventoryItemResponse > GetCachedInventory()
		{
			lock( this._inventoryCacheLock )
			{
				var cachedInventory = this._cache.Get( this._allItemsCacheKey ) as IEnumerable< InventoryItemResponse >;
				if( cachedInventory != null )
					return cachedInventory;
				else
				{
					var items = this.DownloadAllItems().ToList();
					this._cache.Set( this._allItemsCacheKey, items, new CacheItemPolicy { SlidingExpiration = this.SlidingCacheExpiration } );
					return items;
				}
			}
		}

		private IEnumerable< InventoryItemResponse > DownloadAllItems()
		{
			var filter = new ItemsFilter
				{
					DetailLevel = { IncludeClassificationInfo = true, IncludePriceInfo = true, IncludeQuantityInfo = true }
				};

			return this.GetItems( filter );
		}

		private bool UseCache()
		{
			return _cache != null;
		}

		/// <summary>
		/// Gets all items in a list.
		/// </summary>
		/// <returns>Downloads and returns all items.</returns>
		public IList< InventoryItemResponse > GetAllItemsList()
		{
			return this.GetAllItems().ToList();
		}

		/// <summary>
		/// Gets the items by skus.
		/// </summary>
		/// <param name="skus">The skus.</param>
		/// <returns>Enumerator to process items. <c>null</c> is returned
		/// for non-existing skus.</returns>
		/// <remarks>Items are pulled 1 at a time to handle non-existing skus.
		/// This results in slower performance.</remarks>
		public IEnumerable< InventoryItemResponse > GetItems( string[] skus )
		{
			foreach( var sku in skus )
			{
				Profiler.Start( "GetInventoryItemList" );

				var itemList = ActionPolicies.CaGetPolicy.Get(
					() =>
					this._client.GetInventoryItemList( this._credentials, this.AccountId, new[] { sku } ) );

				Profiler.End( "Got SKU - {0}".FormatWith( sku ) );

				if( !IsRequestSuccessful( itemList ) )
				{
					yield return null;
					continue;
				}

				foreach( var item in itemList.ResultData )
				{
					yield return item;
				}
			}
		}

		/// <summary>
		/// Gets the items matching filter.
		/// </summary>
		/// <param name="filter">The filter.</param>
		/// <returns>Items matching supplied filter.</returns>
		/// <seealso href="http://developer.channeladvisor.com/display/cadn/GetFilteredInventoryItemList"/>
		public IEnumerable< InventoryItemResponse > GetItems( ItemsFilter filter )
		{
			filter.Criteria.PageSize = 100;
			filter.Criteria.PageNumber = 0;

			while( true )
			{
				filter.Criteria.PageNumber += 1;
				Profiler.Start( "GetInventoryItemList" );

				var itemResponse = ActionPolicies.CaGetPolicy.Get( () => this._client.GetFilteredInventoryItemList
						(
							this._credentials,
							this.AccountId, filter.Criteria, filter.DetailLevel,
							filter.SortField, filter.SortDirection ) );

				Profiler.End( "Pulled page - " + filter.Criteria.PageNumber );

				if( !IsRequestSuccessful( itemResponse ) )
				{
					yield return null;
					continue;
				}

				var items = itemResponse.ResultData;

				if( items == null )
					yield break;

				foreach( var item in items )
				{
					yield return item;
				}

				if( items.Length == 0 || items.Length < filter.Criteria.PageSize )
					yield break;
			}
		}

		public ClassificationAttributeInfo[] GetAttributes( string sku )
		{
			var attributeList = ActionPolicies.CaGetPolicy.Get(
				() =>
				this._client.GetInventoryItemClassificationAttributeList( this._credentials, this.AccountId, sku ) );
			return GetResultWithSuccessCheck( attributeList, attributeList.ResultData );
		}

		/// <summary>
		/// Gets the additional item quantities.
		/// </summary>
		/// <param name="sku">The sku.</param>
		/// <returns>Item quantities.</returns>
		/// <remarks>This is required since <see cref="GetItems(string[])"/> returns
		/// only available quantity.</remarks>
		/// <see href="http://developer.channeladvisor.com/display/cadn/GetInventoryItemQuantityInfo"/>
		public QuantityInfoResponse GetItemQuantities( string sku )
		{
			var quantityInfo = ActionPolicies.CaGetPolicy.Get( () =>
				this._client.GetInventoryItemQuantityInfo( this._credentials, this.AccountId, sku ));
			return GetResultWithSuccessCheck( quantityInfo, quantityInfo.ResultData );
		}

		/// <summary>
		/// Gets the available quantity.
		/// </summary>
		/// <param name="sku">The sku of the item.</param>
		/// <returns>
		/// The Available quantity for the specified sku.
		/// </returns>
		/// <see href="http://developer.channeladvisor.com/display/cadn/GetInventoryQuantity"/>
		public int GetAvailableQuantity( string sku )
		{
			var quantityResult = ActionPolicies.CaGetPolicy.Get( () => this.InternalGetAvailableQuantity( sku ) );
			return quantityResult.ResultData;
		}

		private APIResultOfInt32 InternalGetAvailableQuantity( string sku )
		{
			var quantityResult = this._client.GetInventoryQuantity( this._credentials, this.AccountId, sku );
			CheckCaSuccess( quantityResult );
			return quantityResult;
		}

		#region  Skus
		public IEnumerable< string > GetAllSkus()
		{
			return this.GetSkus( new ItemsFilter() );
		}

		public IEnumerable< string > GetSkus( ItemsFilter filter )
		{
			filter.Criteria.PageSize = 100;
			filter.Criteria.PageNumber = 0;

			filter.DetailLevel.IncludeClassificationInfo = true;
			filter.DetailLevel.IncludePriceInfo = true;
			filter.DetailLevel.IncludeQuantityInfo = true;

			while( true )
			{
				filter.Criteria.PageNumber += 1;
				var itemResponse = ActionPolicies.CaGetPolicy.Get(
					() => this._client.GetFilteredSkuList
					      	(
					      		this._credentials, this.AccountId, filter.Criteria,
					      		filter.SortField, filter.SortDirection ) );

				if( !IsRequestSuccessful( itemResponse ) )
				{
					yield return null;
					continue;
				}

				var items = itemResponse.ResultData;

				if( items == null )
					yield break;

				foreach( var item in items )
				{
					yield return item;
				}

				if( items.Length == 0 || items.Length < filter.Criteria.PageSize )
					yield break;
			}
		}
		#endregion

		#endregion

		#region Update items
		public void SynchItem( InventoryItemSubmit item )
		{
			ActionPolicies.CaSubmitPolicy.Do(
				() =>
					{
						var resultOfBoolean = this._client.SynchInventoryItem( this._credentials, this.AccountId, item );
						CheckCaSuccess( resultOfBoolean );
					} );
		}

		public void SynchItems( List< InventoryItemSubmit > items )
		{
			// max number of items to submit to CA
			//			const int PageSize = 1000;
			//			const int PageSize = 500;
			const int pageSize = 100;
			var length = pageSize;

			for( int i = 0; i < items.Count; i += pageSize )
			{
				// adjust count of items
				if( i + length > items.Count )
					length = items.Count - i;

				var itemInfoArray = new InventoryItemSubmit[length];
				items.CopyTo( i, itemInfoArray, 0, length );

				Profiler.Start(
					"Synching items, page - {0}, position - {1}, count - {2}".FormatWith(
						Math.Ceiling( ( ( double )i ) / pageSize ), i, itemInfoArray.Length ) );
				
				ActionPolicies.CaSubmitPolicy.Do( () =>
						{
							var resultOfBoolean = this._client.SynchInventoryItemList( this._credentials, this.AccountId, itemInfoArray );
							CheckCaSuccess( resultOfBoolean );
						} );

				Profiler.End();
			}
		}

		public void UpdateQuantityAndPrice( InventoryItemQuantityAndPrice itemQuantityAndPrice )
		{
			ActionPolicies.CaSubmitPolicy.Do( () =>
				{
					var resultOfBoolean = this._client.UpdateInventoryItemQuantityAndPrice( this._credentials, this.AccountId, itemQuantityAndPrice );
					CheckCaSuccess( resultOfBoolean );
				});
		}

		public void UpdateQuantityAndPrices( List< InventoryItemQuantityAndPrice > itemQuantityAndPrices )
		{
			// max number of items to submit to CA
			const int pageSize = 500;
			var length = pageSize;

			for( int i = 0; i < itemQuantityAndPrices.Count; i += pageSize )
			{
				// adjust count of items
				if( i + length > itemQuantityAndPrices.Count )
					length = itemQuantityAndPrices.Count - i;

				var itemInfoArray = new InventoryItemQuantityAndPrice[length];
				itemQuantityAndPrices.CopyTo( i, itemInfoArray, 0, length );

				
				ActionPolicies.CaSubmitPolicy.Do( () =>
					{
						var resultOfBoolean = this._client.UpdateInventoryItemQuantityAndPriceList( this._credentials, this.AccountId, itemInfoArray );
						CheckCaSuccess( resultOfBoolean );
					});
			}
		}
		#endregion

		#region Delete item
		public void DeleteItem( string sku )
		{
			ActionPolicies.CaSubmitPolicy.Do( () =>
				{
					var resultOfBoolean = this._client.DeleteInventoryItem( this._credentials, this.AccountId, sku );
					CheckCaSuccess( resultOfBoolean );
				});
		}
		#endregion

		#region Get Config Info
		public ClassificationConfigurationInformation[] GetClassificationConfigInfo()
		{
			return ActionPolicies.CaGetPolicy.Get( () =>
				{
					var result = this._client.GetClassificationConfigurationInformation( this._credentials, this.AccountId );
					CheckCaSuccess( result );
					return result.ResultData;
				});
		}
		#endregion

		#region  Check for Success
		/// <summary>
		/// Gets the result with success check.
		/// </summary>
		/// <typeparam name="T">Type of the result.</typeparam>
		/// <param name="apiResult">The API result.</param>
		/// <param name="resultData">The result data.</param>
		/// <returns>Returns result default value (typically <c>null</c>) if there was a problem
		/// with API call, otherwise returns result.</returns>
		private static T GetResultWithSuccessCheck< T >( object apiResult, T resultData )
		{
			if( !IsRequestSuccessful( apiResult ) )
				return default( T );

			return resultData;
		}

		/// <summary>
		/// Determines whether request was successful or not.
		/// </summary>
		/// <param name="apiResult">The API result.</param>
		/// <returns>
		/// 	<c>true</c> if request was successful; otherwise, <c>false</c>.
		/// </returns>
		private static bool IsRequestSuccessful( object apiResult )
		{
			var type = apiResult.GetType();

			var statusProp = type.GetProperty( "Status" );
			var status = ( ResultStatus )statusProp.GetValue( apiResult, null );

			var messageCodeProp = type.GetProperty( "MessageCode" );
			var messageCode = ( int )messageCodeProp.GetValue( apiResult, null );

			return status == ResultStatus.Success && messageCode == 0;
		}

		private static void CheckCaSuccess( APIResultOfBoolean apiResult )
		{
			if( apiResult.Status != ResultStatus.Success )
				throw new ChannelAdvisorException( apiResult.MessageCode, apiResult.Message );
		}

		private static void CheckCaSuccess( APIResultOfArrayOfSynchInventoryItemResponse apiResult )
		{
			if( apiResult.Status != ResultStatus.Success )
				throw new ChannelAdvisorException( apiResult.MessageCode, apiResult.Message );
		}

		private static void CheckCaSuccess( APIResultOfArrayOfUpdateInventoryItemResponse apiResult )
		{
			if( apiResult.Status != ResultStatus.Success )
				throw new ChannelAdvisorException( apiResult.MessageCode, apiResult.Message );
		}

		private static void CheckCaSuccess( APIResultOfInt32 quantityResult )
		{
			if( quantityResult.Status != ResultStatus.Success )
				throw new ChannelAdvisorException( quantityResult.MessageCode, quantityResult.Message );
		}

		private static void CheckCaSuccess( APIResultOfArrayOfClassificationConfigurationInformation quantityResult )
		{
			if( quantityResult.Status != ResultStatus.Success )
				throw new ChannelAdvisorException( quantityResult.MessageCode, quantityResult.Message );
		}
		#endregion
	}
}