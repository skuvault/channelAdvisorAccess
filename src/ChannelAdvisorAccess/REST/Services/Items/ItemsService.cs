using ChannelAdvisorAccess.Constants;
using ChannelAdvisorAccess.Exceptions;
using ChannelAdvisorAccess.Misc;
using ChannelAdvisorAccess.REST.Models;
using ChannelAdvisorAccess.Services.Items;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChannelAdvisorAccess.InventoryService;
using ChannelAdvisorAccess.REST.Models.Configuration;
using ChannelAdvisorAccess.REST.Models.Infrastructure;
using ChannelAdvisorAccess.REST.Shared;
using CuttingEdge.Conditions;
using APICredentials = ChannelAdvisorAccess.OrderService.APICredentials;
using ChannelAdvisorAccess.REST.Exceptions;
using ICSharpCode.SharpZipLib.Zip;
using System.IO;
using System.Text;
using CsvHelper;
using System.Collections.Concurrent;
using System.Net;
using System.Globalization;

namespace ChannelAdvisorAccess.REST.Services.Items
{
	public class ItemsService : RestServiceBaseAbstr, IItemsService
	{
		/// <summary>
		///	Channel advisor page size for products end point
		/// </summary>
		private const int pageSizeDefault = 100;
		private const int avgRequestHandlingTimeInSec = 5;
		private const int avgProductExportTimeInSec = 60 * 10;
		private const int productExportWaitTimeInSec = 15;

		private Dictionary< string, int > _productsCache;

		/// <summary>
		///	Rest items service with standard authorization flow
		/// </summary>
		/// <param name="credentials">Rest application credentials</param>
		/// <param name="accountName">Tenant account name</param>
		/// <param name="accessToken">Tenant access token</param>
		/// <param name="refreshToken">Tenant refresh token</param>
		public ItemsService( RestCredentials credentials, string accountName, string accessToken, string refreshToken ) 
			: base( credentials, accountName, accessToken, refreshToken ) { }

		/// <summary>
		///	Rest items service with soap compatible authorization flow
		/// </summary>
		/// <param name="credentials">Rest application credentials</param>
		/// <param name="soapCredentials">Soap application credentials</param>
		/// <param name="accountId">Tenant account id</param>
		/// <param name="accountName">Tenant account name</param>
		public ItemsService( RestCredentials credentials, APICredentials soapCredentials, string accountId, string accountName ) 
			: base( credentials, soapCredentials, accountId, accountName ) { }

		/// <summary>
		///	Set product cache to avoid extra requests which seek product id
		/// </summary>
		/// <param name="productCache"></param>
		public void SetProductCache( Dictionary< string, int > productCache )
		{
			this._productsCache = productCache;
		}

		/// <summary>
		///	Checks asynchronously if sku exists 
		/// </summary>
		/// <param name="sku"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task< bool > DoesSkuExistAsync( string sku, Mark mark = null )
		{
			return await this.GetProductWithIdOnlyBySku( sku, mark ).ConfigureAwait( false ) != null;
		}

		/// <summary>
		///	Checks if sku exists 
		/// </summary>
		/// <param name="sku"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public bool DoesSkuExist( string sku, Mark mark = null )
		{
			return this.DoesSkuExistAsync( sku, mark ).Result;
		}

		/// <summary>
		///	Checks if list of skus exists
		/// </summary>
		/// <param name="skus"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public IEnumerable< DoesSkuExistResponse > DoSkusExist( IEnumerable< string > skus, Mark mark = null )
		{
			return this.DoSkusExistAsync( skus, mark ).Result;
		}

		/// <summary>
		///	Checks asynchronously if list of skus exists
		/// </summary>
		/// <param name="skus"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task< IEnumerable< DoesSkuExistResponse > > DoSkusExistAsync( IEnumerable< string > skus, Mark mark = null )
		{
			var response = new List< DoesSkuExistResponse >();

			if ( skus.Count() == 0 )
				return response;

			var productIds = await this.GetProductsId( skus, mark ).ConfigureAwait( false );

			foreach( var sku in skus )
			{
				var doesSkuExist = productIds.ContainsKey( sku.ToLower() );
				response.Add( new DoesSkuExistResponse() { Result = doesSkuExist, Sku = sku } );
			}

			return response;
		}

		public async Task< Dictionary< string, int > > GetProductsId( IEnumerable< string > skus, Mark mark )
		{
			if ( _productsCache != null )
			{
				return GetProductsWithIdOnlyFromCache( skus );
			}

			double estimateRequestPerSkuTotalProcessingTimeInSec = Math.Ceiling( (double)skus.Count() / _maxBatchSize ) * avgRequestHandlingTimeInSec;

			// use product export if it saves time
			if ( estimateRequestPerSkuTotalProcessingTimeInSec > avgProductExportTimeInSec )
			{
				try
				{
					return await this.ImportProducts( mark ).ConfigureAwait( false );
				}
				catch( ChannelAdvisorProductExportUnavailableException ) { }
			}

			if ( skus.Count() == 1 )
			{
				var product = await GetProductWithIdOnlyBySku( skus.First(), mark ).ConfigureAwait( false );

				if ( product != null )
				{
					return new Dictionary< string, int >() { { product.Sku.ToLower(), product.ID } };
				}
				
				return new Dictionary< string, int >();
			}

			return await GetProductsWithIdOnlyByBatch( skus, mark ).ConfigureAwait( false );
		}

		private async Task< Dictionary< string, int > > GetProductsWithIdOnlyByBatch( IEnumerable< string > skus, Mark mark )
		{
			var productIds = new Dictionary< string, int >();
			var batchBuilder = new BatchBuilder( ChannelAdvisorEndPoint.BaseApiUrl + "/" );
			var urlBuilder = new ItemsServiceUrlBuilder();

			foreach( var sku in skus )
			{
				batchBuilder.AddGetRequest( urlBuilder.GetProductWithIdOnlyBySkuUrl( sku ) );
			}

			var productResponses = await base.DoBatch< ODataResponse< Product > >( batchBuilder, mark ).ConfigureAwait( false );
			var products = productResponses.Where( r => r.Value.Length != 0 ).Select( r => r.Value.First() );

			foreach( var product in products )
			{
				var sku = product.Sku.ToLower();

				if ( !productIds.ContainsKey( sku ) )
				{
					productIds.Add( sku, product.ID );
				}
			}

			return productIds;
		}

		private Dictionary< string, int > GetProductsWithIdOnlyFromCache( IEnumerable< string > skus )
		{
			var productsId = new Dictionary< string, int >();

			foreach( string sku in skus )
			{
				var tmp = sku.ToLower();

				if ( _productsCache.ContainsKey( tmp ) )
				{
					int productId = _productsCache[ tmp ];
					productsId.Add( tmp, productId );
				}
			}

			return productsId;
		}

		/// <summary>
		///	Gets all items from stock
		/// </summary>
		/// <param name="mark"></param>
		/// <returns></returns>
		public IEnumerable< InventoryItemResponse > GetAllItems( Mark mark = null )
		{
			return this.GetAllItemsAsync( mark ).Result;
		}

		/// <summary>
		///	Get all items asynchronously from stock
		/// </summary>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task< IEnumerable < InventoryItemResponse > > GetAllItemsAsync( Mark mark = null )
		{
			var result = await this.GetProductsWithAllProperties( null, mark ).ConfigureAwait( false );

			return result.Response.Select( product => product.ToInventoryItemResponse() );
		}

		/// <summary>
		///	Gets all skus
		/// </summary>
		/// <param name="mark"></param>
		/// <returns></returns>
		public IEnumerable< string > GetAllSkus( Mark mark = null )
		{
			return this.GetAllSkusAsync( mark ).Result;
		}

		/// <summary>
		///	Gets all skus asynchronously
		/// </summary>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task< IEnumerable< string > > GetAllSkusAsync( Mark mark = null )
		{
			var skus = new List< string >();
			int pageNumber = 0;

			while (true)
			{
				var url = new ItemsServiceUrlBuilder().GetProductsUrl( "", "ID,Sku", null );
				var page = await this.GetProducts( url, mark, pageNumber ).ConfigureAwait( false );
				skus.AddRange( page.Response.Select( pr => pr.Sku ) );

				if ( page.AllPagesQueried )
					break;

				pageNumber++;
			}

			return skus;
		}

		/// <summary>
		///	Gets available quantity for each sku
		/// </summary>
		/// <param name="skus"></param>
		/// <param name="mark"></param>
		/// <param name="delatInMs"></param>
		/// <returns></returns>
		public IEnumerable< InventoryQuantityResponse > GetAvailableQuantities( IEnumerable< string > skus, Mark mark = null, int delatInMs = 5000 )
		{
			return this.GetAvailableQuantitiesAsync( skus, mark ).Result;
		}

		/// <summary>
		///	Returns skus available quantity
		/// </summary>
		/// <param name="skus"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task< IEnumerable< InventoryQuantityResponse > > GetAvailableQuantitiesAsync( IEnumerable< string > skus, Mark mark = null )
		{
			var inventoryQuantity = new List< InventoryQuantityResponse >();
			var batchBuilder = new BatchBuilder( ChannelAdvisorEndPoint.BaseApiUrl + "/" );
			var urlBuilder = new ItemsServiceUrlBuilder();

			var urls = skus.Select( s => urlBuilder.GetProductWithQuantityOnlyBySkuUrl( s ) );
			batchBuilder.AddGetRequests( urls );

			var responses = await base.DoBatch< ODataResponse< Product > >( batchBuilder, mark ).ConfigureAwait( false );
			var skusQuantities = responses.Where( r => r.Value.Length > 0 ).Select( r => r.Value.FirstOrDefault() );

			foreach( var sku in skus )
			{
				var product = skusQuantities.FirstOrDefault( sq => sq.Sku.ToLower().Equals( sku.ToLower() ) );

				if ( product == null )
					inventoryQuantity.Add( new InventoryQuantityResponse() { SKU = sku, MessageCode = 113, Message = String.Format( "The specified SKU {0} does not exist", sku ) });
				else
					inventoryQuantity.Add( new InventoryQuantityResponse() { SKU = sku, Quantity = product.TotalAvailableQuantity });
			}

			return inventoryQuantity.ToArray();
		}

		/// <summary>
		///	Gets sku available quantity
		/// </summary>
		/// <param name="sku"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public int GetAvailableQuantity( string sku, Mark mark = null )
		{
			return this.GetAvailableQuantityAsync( sku, mark ).Result;
		}

		/// <summary>
		///	Gets sku available quantity asynchronously
		/// </summary>
		/// <param name="sku"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task< int > GetAvailableQuantityAsync( string sku, Mark mark = null )
		{
			var product = await this.GetProductWithQuantityOnlyBySku( sku, mark ).ConfigureAwait( false );

			if ( product != null )
				return product.TotalAvailableQuantity;

			return 0;
		}

		/// <summary>
		///	Gets distribution centers list
		/// </summary>
		/// <param name="mark"></param>
		/// <returns></returns>
		public DistributionCenterResponse[] GetDistributionCenterList( Mark mark = null )
		{
			return this.GetDistributionCenterListAsync( mark ).Result;
		}

		/// <summary>
		///	Gets distribution centers list asynchronously
		/// </summary>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task< DistributionCenterResponse[] > GetDistributionCenterListAsync( Mark mark = null )
		{
			var response = await this.GetDistributionCentersAsync( mark ).ConfigureAwait( false );
			return response.Select( dc => dc.ToDistributionCenterResponse() ).ToArray();
		}

		/// <summary>
		///	Get distribution centers
		/// </summary>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task< DistributionCenter[] > GetDistributionCentersAsync( Mark mark = null )
		{
			var distributionCenters = new List< DistributionCenter >();

			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );
				var result = await base.GetResponseAsync< DistributionCenter >( ChannelAdvisorEndPoint.DistributionCentersUrl, mark );
				distributionCenters.AddRange( result.Response.Where( dc => !dc.IsDeleted ) );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : distributionCenters.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}

			return distributionCenters.ToArray();
		}

		/// <summary>
		///	Return distribution center info by code
		/// </summary>
		/// <param name="code"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task< DistributionCenter > GetDistributionCenterAsync( string code, Mark mark )
		{
			var distributionCenters = await GetDistributionCentersAsync( mark ).ConfigureAwait( false );
			return distributionCenters.FirstOrDefault( dc => dc.Code.ToLower().Equals( code.ToLower() ) );
		}

		/// <summary>
		///  Gets SKU quantity
		/// </summary>
		/// <param name="sku"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public QuantityInfoResponse GetItemQuantities( string sku, Mark mark = null )
		{
			return this.GetItemQuantitiesAsync( sku, mark ).Result;
		}

		/// <summary>
		/// Gets SKU quantity asynchronously
		/// </summary>
		/// <param name="sku"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task< QuantityInfoResponse > GetItemQuantitiesAsync( string sku, Mark mark = null )
		{
			var product = await this.GetProductBySku( sku, mark ).ConfigureAwait ( false );

			if ( product != null )
				return product.ToQuantityInfoResponse();

			return null;
		}

		/// <summary>
		/// Gets items
		/// </summary>
		/// <param name="skus"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public IEnumerable< InventoryItemResponse > GetItems( IEnumerable< string > skus, Mark mark = null )
		{
			return this.GetItemsAsync( skus, mark ).Result;
		}

		/// <summary>
		///  Gets items asynchronously
		/// </summary>
		/// <param name="skus"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task< IEnumerable< InventoryItemResponse > > GetItemsAsync( IEnumerable< string > skus, Mark mark = null )
		{
			var batchBuilder = new BatchBuilder( ChannelAdvisorEndPoint.BaseApiUrl + "/" );
			var urlBuilder = new ItemsServiceUrlBuilder();

			foreach( var sku in skus )
			{
				batchBuilder.AddGetRequest( urlBuilder.GetProductBySkuUrl( sku, null, null ) );
			}

			var productResponses = await base.DoBatch< ODataResponse< Product > >( batchBuilder, mark ).ConfigureAwait( false );

			return productResponses.Where( r => r.Value.Length != 0 ).Select( r => r.Value.First().ToInventoryItemResponse() );
		}

		/// <summary>
		///	Updates sku quantity in CA
		/// </summary>
		/// <param name="itemQuantityAndPrice"></param>
		/// <param name="mark"></param>
		public void UpdateQuantityAndPrice( InventoryItemQuantityAndPrice itemQuantityAndPrice, Mark mark = null )
		{
			this.UpdateQuantityAndPriceAsync( itemQuantityAndPrice, mark ).GetAwaiter().GetResult();
		}

		/// <summary>
		///	Updates sku quantity asynchronously in CA
		/// </summary>
		/// <param name="itemQuantityAndPrice"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task UpdateQuantityAndPriceAsync( InventoryItemQuantityAndPrice itemQuantityAndPrice, Mark mark = null )
		{
			Condition.Requires( itemQuantityAndPrice ).IsNotNull();
			Condition.Requires( itemQuantityAndPrice.Quantity ).IsNotNull();

			var product = await this.GetProductWithIdOnlyBySku( itemQuantityAndPrice.Sku, mark ).ConfigureAwait( false );

			if ( product != null )
			{
				var distributionCenter = await this.GetDistributionCenterAsync( itemQuantityAndPrice.DistributionCenterCode, mark );

				if (distributionCenter != null && !distributionCenter.IsExternallyManaged ) // cannot update quantity on externally managed warehouse
				{
					var request = new UpdateQuantityRequest()
					{
						UpdateType = itemQuantityAndPrice.UpdateType,
						Updates = new UpdateQuantityDC[] { new UpdateQuantityDC() { DistributionCenterID = distributionCenter.ID, Quantity = itemQuantityAndPrice.Quantity.Value } }
					};

					try
					{
						var url = new ItemsServiceUrlBuilder().GetUpdateProductQuantityUrl( product.ID );
						ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodParameters: url, additionalInfo : this.AdditionalLogInfo() ) );
						await base.PostAsync( url, new { Value = request }, mark ).ConfigureAwait( false );
						
						ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodParameters: url, methodResult: request.ToJson(), additionalInfo : this.AdditionalLogInfo()) );
					}
					catch( Exception exception )
					{
						throw this.HandleExceptionAndLog( mark, exception );
					}
				}
			}
		}

		/// <summary>
		///	Updates skus quantities in CA
		/// </summary>
		/// <param name="itemQuantityAndPrices"></param>
		/// <param name="mark"></param>
		public void UpdateQuantityAndPrices( IEnumerable< InventoryItemQuantityAndPrice > itemQuantityAndPrices, Mark mark = null)
		{
			this.UpdateQuantityAndPricesAsync( itemQuantityAndPrices, mark ).GetAwaiter().GetResult();
		}

		/// <summary>
		///	Updates skus quantities asynchronously in CA
		/// </summary>
		/// <param name="itemQuantityAndPrices"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task UpdateQuantityAndPricesAsync( IEnumerable< InventoryItemQuantityAndPrice > itemQuantityAndPrices, Mark mark = null )
		{
			var distributionCenters = await this.GetDistributionCentersAsync( mark ).ConfigureAwait( false );
			var productsId = await this.GetProductsId( itemQuantityAndPrices.Select( iq => iq.Sku ), mark ).ConfigureAwait( false );
			var batch = new BatchBuilder( ChannelAdvisorEndPoint.BaseApiUrl + "/" );
			var urlBuilder = new ItemsServiceUrlBuilder();

			foreach( var itemQuantity in itemQuantityAndPrices )
			{
				if ( !productsId.ContainsKey( itemQuantity.Sku.ToLower() ) )
					continue;

				int productId = productsId[ itemQuantity.Sku.ToLower() ];
				var distributionCenter = distributionCenters.FirstOrDefault( dc => dc.Code.ToLower().Equals( itemQuantity.DistributionCenterCode.ToLower() ) );

				if ( distributionCenter == null )
					continue;

				var url = urlBuilder.GetUpdateProductQuantityUrl( productId );

				var request = new UpdateQuantityRequest()
				{
					UpdateType = itemQuantity.UpdateType,
					Updates = new UpdateQuantityDC[] { new UpdateQuantityDC() { DistributionCenterID = distributionCenter.ID, Quantity = itemQuantity.Quantity.Value } }
				};
				
				batch.AddPostRequest( url, new { Value = request }.ToJson() );
			}

			await base.DoBatch< ODataResponse <Product> >( batch, mark ).ConfigureAwait( false );
		}

		/// <summary>
		///	Gets sku store info (deprecated in the REST API)
		/// </summary>
		/// <param name="sku"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public StoreInfo GetStoreInfo( string sku, Mark mark = null )
		{
			return this.GetStoreInfoAsync( sku, mark ).Result;
		}

		/// <summary>
		///	Gets sku quantity in the each distribution center
		/// </summary>
		/// <param name="sku"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public DistributionCenterInfoResponse[] GetShippingInfo( string sku, Mark mark = null )
		{
			return this.GetShippingInfoAsync( sku, mark ).Result;
		}

		/// <summary>
		///	Gets sku quantity asynchronously in the each distribution center
		/// </summary>
		/// <param name="sku"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task< DistributionCenterInfoResponse[] > GetShippingInfoAsync( string sku, Mark mark = null )
		{
			var response = new List< DistributionCenterInfoResponse >();
			var product = await this.GetProductWithQuantityOnlyBySku( sku, mark ).ConfigureAwait( false );

			if ( product != null )
			{
				var distributionCenters = await this.GetDistributionCentersAsync( mark ).ConfigureAwait( false );

				foreach( var dcQuantity in product.DCQuantities )
				{
					var distributionCenter = distributionCenters.FirstOrDefault( dc => dc.ID == dcQuantity.DistributionCenterID );

					if ( distributionCenter != null )
					{
						response.Add( new DistributionCenterInfoResponse()
						{
							AvailableQuantity = dcQuantity.AvailableQuantity,
							DistributionCenterCode = distributionCenter.Code
						} );
					}
				}
			}

			return response.ToArray();
		}

		/// <summary>
		///	Gets sku store info asynchronously (deprecated in the REST API)
		/// </summary>
		/// <param name="sku"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task< StoreInfo > GetStoreInfoAsync( string sku, Mark mark = null )
		{
			var product = await this.GetProductWithStoreInfoOnlyBySku( sku, mark ).ConfigureAwait( false );

			if ( product != null )
			{
				return new StoreInfo()
				{
					 Title = product.StoreTitle,
					 Description = product.StoreDescription,
					 DisplayInStore = product.IsDisplayInStore
				};
			}

			return null;
		}

		/// <summary>
		///	Gets sku attributes
		/// </summary>
		/// <param name="sku"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public AttributeInfo[] GetAttributes( string sku, Mark mark = null )
		{
			return this.GetAttributesAsync( sku, mark ).Result;
		}

		/// <summary>
		///	Gets sku attributes asynchronously
		/// </summary>
		/// <param name="sku"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task< AttributeInfo[] > GetAttributesAsync( string sku, Mark mark = null )
		{
			var attributes = new List< AttributeInfo >();

			var product = await this.GetProductWithAttributesOnlyBySku( sku, mark ).ConfigureAwait( false );

			if ( product != null )
			{
				foreach( var attributeValue in product.Attributes )
				{
					attributes.Add( new AttributeInfo()
					{
						Name = attributeValue.Name,
						Value = attributeValue.Value
					});
				}
			}

			return attributes.ToArray();
		}

		/// <summary>
		///	Gets sku variation
		/// </summary>
		/// <param name="sku"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public VariationInfo GetVariationInfo( string sku, Mark mark = null )
		{
			return this.GetVariationInfoAsync( sku, mark ).Result;
		}

		/// <summary>
		///	Gets sku variation asynchronously
		/// </summary>
		/// <param name="sku"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task< VariationInfo > GetVariationInfoAsync( string sku, Mark mark = null )
		{
			var product = await this.GetProductWithVariationInfoOnlyBySku( sku, mark ).ConfigureAwait( false );

			if ( product != null )
			{
				var variationInfo = new VariationInfo()
				{
					 IsInRelationship = product.IsInRelationship,
					 IsParent = product.IsParent,
					 RelationshipName = product.RelationshipName
				};
				
				if ( product.ParentProductID != null )
				{
					var parentProduct = await this.GetProductById( product.ParentProductID.Value, mark ).ConfigureAwait( false );

					if ( parentProduct != null )
						variationInfo.ParentSku = parentProduct.Sku;
				}

				return variationInfo;
			}

			return null;
		}

		/// <summary>
		///	Gets sku images
		/// </summary>
		/// <param name="sku"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public ImageInfoResponse[] GetImageList( string sku, Mark mark = null )
		{
			return this.GetImageListAsync( sku, mark ).Result;
		}

		/// <summary>
		///	Gets sku images asynchronously
		/// </summary>
		/// <param name="sku"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task< ImageInfoResponse[] > GetImageListAsync( string sku, Mark mark = null )
		{
			var productImages = new List< ImageInfoResponse >();
			var product = await this.GetProductBySku( sku, "ID,Sku,Images", "Images", mark ).ConfigureAwait( false );

			if ( product != null && product.Images != null )
			{
				foreach( var image in product.Images )
					productImages.Add ( new ImageInfoResponse()
					{
						Url = image.Url,
						PlacementName = image.PlacementName
					});
			}

			return productImages.ToArray();
		}

		/// <summary>
		///	Gets filtered items
		/// </summary>
		/// <param name="filter"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public IEnumerable< InventoryItemResponse > GetFilteredItems( ItemsFilter filter, Mark mark = null )
		{
			return this.GetFilteredItemsAsync( filter, mark ).Result;
		}

		/// <summary>
		///	Gets filtered items asynchronously
		/// </summary>
		/// <param name="filter"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task< IEnumerable< InventoryItemResponse > > GetFilteredItemsAsync( ItemsFilter filter, Mark mark = null )
		{
			var pagedResult = await this.GetFilteredItemsAsync( filter, 0, pageSizeDefault, mark ).ConfigureAwait( false );

			return pagedResult.Response;
		}

		/// <summary>
		///	Gets filtered items asynchronously with extra page information
		/// </summary>
		/// <param name="filter"></param>
		/// <param name="pageNumber"></param>
		/// <param name="pageLimit"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task< PagedApiResponse< InventoryItemResponse > > GetFilteredItemsAsync( ItemsFilter filter, int pageNumber, int pageLimit = pageSizeDefault, Mark mark = null )
		{
			var inventoryItems = new List< InventoryItemResponse >();
			var url = new ItemsServiceUrlBuilder().GetProductsUrl( filter, null, null );

			var result = await this.GetProducts( url, mark, pageNumber: pageNumber, pageSize: pageLimit ).ConfigureAwait( false );

			foreach( var product in result.Response )
				inventoryItems.Add( product.ToInventoryItemResponse() );

			return new PagedApiResponse< InventoryItemResponse >( inventoryItems, result.FinalPageNumber, result.AllPagesQueried );
		}

		/// <summary>
		///	Gets filtered skus
		/// </summary>
		/// <param name="filter"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public IEnumerable< string > GetFilteredSkus( ItemsFilter filter, Mark mark = null )
		{
			return this.GetFilteredSkusAsync( filter, mark ).Result;
		}

		/// <summary>
		///	Gets filtered skus asynchronously
		/// </summary>
		/// <param name="filter"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task< IEnumerable< string > > GetFilteredSkusAsync( ItemsFilter filter, Mark mark = null )
		{
			return ( await this.GetFilteredSkusAsync( filter, 0, pageSizeDefault, mark ).ConfigureAwait( false ) ).Response;
		}

		/// <summary>
		///	Gets filtered skus asynchronously with extra page information
		/// </summary>
		/// <param name="filter"></param>
		/// <param name="startPage"></param>
		/// <param name="pageLimit"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task< PagedApiResponse< string > > GetFilteredSkusAsync( ItemsFilter filter, int startPage, int pageLimit, Mark mark = null )
		{
			var url = new ItemsServiceUrlBuilder().GetProductsUrl( filter, "ID,Sku", null );
			var result = await this.GetProducts( url, mark, pageNumber: startPage, pageSize: pageLimit ).ConfigureAwait( false );

			return new PagedApiResponse< string >( result.Response.Select( item => item.Sku ), result.FinalPageNumber, result.AllPagesQueried );
		}
		
		/// <summary>
		///	Updates product field
		/// </summary>
		/// <param name="item"></param>
		/// <param name="isCreateNew"></param>
		/// <param name="mark"></param>
		public void SynchItem( InventoryItemSubmit item, bool isCreateNew = false, Mark mark = null )
		{
			this.SynchItemAsync( item, isCreateNew, mark ).GetAwaiter().GetResult();
		}

		/// <summary>
		///	Updates product fields asynchronously
		/// </summary>
		/// <param name="item"></param>
		/// <param name="isCreateNew"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task SynchItemAsync( InventoryItemSubmit item, bool isCreateNew = false, Mark mark = null )
		{
			var product = await this.GetProductWithIdOnlyBySku( item.Sku, mark ).ConfigureAwait( false );

			if ( product == null )
				return;

			var request = this.GetUpdateProductFieldsRequest( item );

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );

				var url = new ItemsServiceUrlBuilder().GetUpdateProductUrl( product.ID );
				await base.PutAsync( url, request ).ConfigureAwait( false );
						
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo()) );
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		/// <summary>
		///	Updates products fields
		/// </summary>
		/// <param name="items"></param>
		/// <param name="isCreateNew"></param>
		/// <param name="mark"></param>
		public void SynchItems( IEnumerable< InventoryItemSubmit > items, bool isCreateNew = false, Mark mark = null )
		{
			SynchItemsAsync( items, isCreateNew, mark ).GetAwaiter().GetResult();
		}

		/// <summary>
		///	Updates products fields asynchronously
		/// </summary>
		/// <param name="items"></param>
		/// <param name="isCreateNew"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task SynchItemsAsync( IEnumerable< InventoryItemSubmit > items, bool isCreateNew = false, Mark mark = null )
		{
			var productsId = await this.GetProductsId( items.Select( i => i.Sku ), mark ).ConfigureAwait( false );
			var batch = new BatchBuilder( ChannelAdvisorEndPoint.BaseApiUrl + "/" );
			var urlBuilder = new ItemsServiceUrlBuilder();
			
			foreach( var item in items )
			{
				if ( !productsId.ContainsKey( item.Sku.ToLower() ) )
					continue;

				int productId = productsId[ item.Sku.ToLower() ];

				var url = urlBuilder.GetUpdateProductUrl( productId );
				var payload = GetUpdateProductFieldsRequest( item );
				batch.AddPutRequest( url, payload.ToJson() );
			}

			await base.DoBatch< ODataResponse< Product > >( batch, mark ).ConfigureAwait( false );
		}

		/// <summary>
		///	Search product by sku
		/// </summary>
		/// <param name="sku"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		private Task< Product > GetProductBySku( string sku, Mark mark )
		{
			return this.GetProductBySku( sku, null, null, mark );
		}

		/// <summary>
		///	Search product by sku and limit properties by id
		/// </summary>
		/// <param name="sku"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		private Task< Product > GetProductWithIdOnlyBySku( string sku, Mark mark )
		{
			var url = new ItemsServiceUrlBuilder().GetProductWithIdOnlyBySkuUrl( sku );
			return this.GetProductBySku( sku, url, mark );
		}

		/// <summary>
		///	Search product by sku and limit properties by store information
		/// </summary>
		/// <param name="sku"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		private Task< Product > GetProductWithStoreInfoOnlyBySku( string sku, Mark mark )
		{
			var url = new ItemsServiceUrlBuilder().GetProductWithStoreInfoOnlyBySkuUrl( sku );
			return this.GetProductBySku( sku, url, mark );
		}

		/// <summary>
		///	Search product by sku and limit properties by attributes
		/// </summary>
		/// <param name="sku"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		private Task< Product > GetProductWithAttributesOnlyBySku( string sku, Mark mark )
		{
			var url = new ItemsServiceUrlBuilder().GetProductWithAttributesOnlyBySkuUrl( sku );
			return this.GetProductBySku( sku, url, mark );
		}

		/// <summary>
		///	Search product by sku and limit properties by variation info
		/// </summary>
		/// <param name="sku"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		private Task< Product > GetProductWithVariationInfoOnlyBySku( string sku, Mark mark )
		{
			var url = new ItemsServiceUrlBuilder().GetProductWithVariationInfoOnlyBySkuUrl( sku );
			return this.GetProductBySku( sku, url, mark );
		}
		
		/// <summary>
		///	Search product by sku and limit properties by quantity
		/// </summary>
		/// <param name="sku"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		private Task< Product > GetProductWithQuantityOnlyBySku( string sku, Mark mark )
		{
			var url = new ItemsServiceUrlBuilder().GetProductWithQuantityOnlyBySkuUrl( sku );
			return this.GetProductBySku( sku, url, mark );
		}

		/// <summary>
		///	Search product
		/// </summary>
		/// <param name="filterProperties"></param>
		/// <param name="mark"></param>
		/// <param name="pageNumber"></param>
		/// <param name="pageSize"></param>
		/// <returns></returns>
		private Task< PagedApiResponse< Product > > GetProductsWithAllProperties( string filterProperties, Mark mark, int pageNumber = 0, int pageSize = pageSizeDefault )
		{
			var url = this.GetProductsUrl( filterProperties, "DCQuantities,Attributes,Images", null );
			return this.GetProducts( url, mark, pageNumber, pageSize );
		}

		/// <summary>
		///	Search product by sku
		/// </summary>
		/// <param name="sku"></param>
		/// <param name="selectProperties"></param>
		/// <param name="expandProperties"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		private async Task< Product > GetProductBySku( string sku, string selectProperties, string expandProperties, Mark mark )
		{
			var url = this.GetProductBySkuUrl( sku, selectProperties, expandProperties );
			var result = await this.GetProducts( url, mark ).ConfigureAwait( false );
			
			return result.Response.FirstOrDefault( pr => pr.Sku.ToLower().Equals( sku.ToLower() ) );
		}

		private async Task< Product > GetProductBySku( string sku, string url, Mark mark )
		{
			var result = await this.GetProducts( url, mark ).ConfigureAwait( false );
			return result.Response.FirstOrDefault( pr => pr.Sku.ToLower().Equals( sku.ToLower() ) );
		}

		private string GetProductBySkuUrl( string sku, string selectProperties, string expandProperties )
		{
			// following OData 4.0 specification single quote char should be represented as two single quotes
			var filter = string.Format( "sku eq '{0}'", Uri.EscapeDataString( sku.Replace( "'", "''" ) ) );

			return GetUrl( ChannelAdvisorEndPoint.ProductsUrl, filter, selectProperties, expandProperties );
		}

		private string GetProductsUrl( string filterProperties, string selectProperties, string expandProperties )
		{
			return GetUrl( ChannelAdvisorEndPoint.ProductsUrl, filterProperties, selectProperties, expandProperties );
		}

		/// <summary>
		///	Build url for specified endpoint
		/// </summary>
		/// <param name="baseUrl"></param>
		/// <param name="filterProperties"></param>
		/// <param name="selectProperties"></param>
		/// <param name="expandProperties"></param>
		/// <returns></returns>
		private string GetUrl( string baseUrl, string filterProperties, string selectProperties, string expandProperties )
		{
			var requestParams = new List< string >();

			// filter products by sku
			if ( !string.IsNullOrEmpty( filterProperties ) )
			{
				requestParams.Add( string.Format( "$filter={0}", filterProperties ) );
			}

			// expand product properties
			if ( !string.IsNullOrEmpty( expandProperties ) )
			{
				requestParams.Add( string.Format( "$expand={0}", expandProperties ) );
			}

			// select only specified properties
			if ( !string.IsNullOrEmpty( selectProperties ) )
			{
				requestParams.Add( string.Format( "$select={0}", selectProperties ) );
			}

			return baseUrl + "?" + string.Join( "&", requestParams.ToArray() );
		}

		/// <summary>
		///	Gets products
		/// </summary>
		/// <param name="url"></param>
		/// <param name="mark"></param>
		/// <param name="pageNumber"></param>
		/// <param name="pageSize"></param>
		/// <returns></returns>
		private async Task< PagedApiResponse< Product > > GetProducts( string url, Mark mark, int pageNumber = 0, int pageSize = pageSizeDefault )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			PagedApiResponse< Product > result = null;

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters: url ) );

				result = await base.GetResponseAsync< Product >( url, mark, pageNumber: pageNumber, pageSize: pageSize ).ConfigureAwait( false );

				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, 
					methodParameters: url,
					methodResult : result.ToJson(), 
					additionalInfo : this.AdditionalLogInfo() ) );
				
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}

			return result;
		}

		/// <summary>
		///	Import products from ChannelAdvisor
		/// </summary>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task< Dictionary< string, int > > ImportProducts( Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			string csvHeader = "id, sku";
			string responseFileUrl = null;

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters: ChannelAdvisorEndPoint.ProductExportUrl ) );
				var response = await this.PostAsyncAndGetResult< ProductExportResponse >( ChannelAdvisorEndPoint.ProductExportUrl, csvHeader, mark ).ConfigureAwait( false );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodParameters: ChannelAdvisorEndPoint.ProductExportUrl, methodResult : response.ToJson(), additionalInfo : this.AdditionalLogInfo() ) );

				ThrowIfProductExportFailed ( response.Status );
				responseFileUrl = response.ResponseFileUrl;
				string url = ChannelAdvisorEndPoint.ProductExportUrl + "?token=" + response.Token;

				// waiting for export result
				while ( responseFileUrl == null )
				{
					// wait 15 seconds and ask job status
					await Task.Delay( productExportWaitTimeInSec * 1000 ).ConfigureAwait( false );

					ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters: url ) );

					var exportStatusResponse = await this.GetEntityAsync< ProductExportResponse >( url, mark ).ConfigureAwait( false );

					ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, 
						methodParameters: url,
						methodResult : exportStatusResponse.ToJson(), 
						additionalInfo : this.AdditionalLogInfo() ) );

					ThrowIfProductExportFailed ( exportStatusResponse.Status );

					if ( exportStatusResponse.ResponseFileUrl != null )
					{
						responseFileUrl = exportStatusResponse.ResponseFileUrl;

						// handle result
						var fileData = await this.DownloadFile( responseFileUrl ).ConfigureAwait( false );
						return ReadZippedCsv( fileData );
					}
				}
			}
			catch( Exception exception )
			{
				var channelAdvisorEx = exception as ChannelAdvisorException;
				var tooManyRequestsErrorCode = 429;

				if ( channelAdvisorEx != null 
						&& ( channelAdvisorEx.MessageCode == (int)HttpStatusCode.BadRequest 
							|| channelAdvisorEx.MessageCode == tooManyRequestsErrorCode 
							|| channelAdvisorEx.MessageCode == (int)HttpStatusCode.InternalServerError ) )
					throw new ChannelAdvisorProductExportUnavailableException( channelAdvisorEx.Message, channelAdvisorEx );

				throw this.HandleExceptionAndLog( mark, exception );
			}

			return new Dictionary< string, int >();
		}

		/// <summary>
		///	Read zipped csv
		/// </summary>
		/// <param name="fileData"></param>
		/// <returns></returns>
		private Dictionary< string, int > ReadZippedCsv( byte[] fileData )
		{
			using( var memoryStream = new MemoryStream( fileData ) )
			{
				using( var zipFile = new ZipFile( memoryStream ) )
				{
					foreach ( ZipEntry zipEntry in zipFile ) {
						if ( !zipEntry.IsFile ) {
							continue;
						}

						var zipStream = zipFile.GetInputStream( zipEntry );

						using( var streamReader = new StreamReader( zipStream, Encoding.UTF8 ) )
						{
							using( var csvReader = new CsvReader( streamReader ))
							{
								csvReader.Configuration.HasHeaderRecord = true;
								csvReader.Configuration.Delimiter = "\t";
								csvReader.Configuration.BadDataFound = null;

								var rows = csvReader.GetRecords< ProductExportRow >();
								var productIds = new Dictionary< string, int >();

								foreach( var row in rows )
								{
									string sku = row.Sku.ToLower();

									if ( !productIds.ContainsKey( sku ) )
									{
										productIds.Add( sku, row.ID );
									}
								}

								return productIds;
							}
						}
					}
				}
			}

			return new Dictionary< string, int >();
		}

		/// <summary>
		///	Check product export job status
		/// </summary>
		/// <param name="status"></param>
		private void ThrowIfProductExportFailed( ImportStatus status )
		{
			if ( status == ImportStatus.Complete
				|| status == ImportStatus.InProgress
				|| status == ImportStatus.InProgressPartitioning
				|| status == ImportStatus.InProgressProcessing
				|| status == ImportStatus.InProgressQueuedforProcessing
				|| status == ImportStatus.InProgressValidation
				|| status == ImportStatus.Pending
				|| status == ImportStatus.PendingPartitioning )
				return;

			throw new ChannelAdvisorProductExportUnavailableException( status.ToString() );
		}

		/// <summary>
		///	Returns total products number
		/// </summary>
		/// <param name="mark"></param>
		/// <returns></returns>
		public async Task< int > GetCatalogSize( Mark mark )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			int total = 0;

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters: ChannelAdvisorEndPoint.ProductsUrl ) );

				total = ( await base.GetEntityAsync< int >( ChannelAdvisorEndPoint.ProductsUrl + "/$count" ).ConfigureAwait( false ) );
				
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, 
					methodParameters: ChannelAdvisorEndPoint.ProductsUrl,
					methodResult : total.ToJson() , 
					additionalInfo : this.AdditionalLogInfo() ) );
				
			}
			catch(Exception exception)
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}

			return total;
		}
	
		/// <summary>
		///	Gets product by ID
		/// </summary>
		/// <param name="productId"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		private async Task< Product > GetProductById ( int productId, Mark mark )
		{
			Product product = null;

			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			var url = ChannelAdvisorEndPoint.ProductsUrl + "(" + productId.ToString() + ")";

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo() ) );

				var result = await base.GetResponseAsync< Product >( url, mark, false ).ConfigureAwait( false );
				product = result.Response.FirstOrDefault();

				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, 
													  methodResult : result.ToJson(), 
													  additionalInfo : this.AdditionalLogInfo() ) );
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}

			return product;
		}
	
		private Dictionary< string, object > GetUpdateProductFieldsRequest( InventoryItemSubmit item )
		{
			var payload = new Dictionary< string, object >();

			if ( item.UPC != null )
			{
				payload.Add( "UPC", item.UPC );
			}

			if ( item.PriceInfo != null )
			{
				payload.Add( "Cost", item.PriceInfo.Cost );
			}

			if ( item.WarehouseLocation != null )
			{
				payload.Add( "WarehouseLocation", item.WarehouseLocation );
			}

			return payload;
		}

		public void Ping(Mark mark = null)
		{
		}

		public Task PingAsync(Mark mark = null)
		{
			return Task.Run(() => { });
		}

		public void RemoveLabelListFromItemList(string[] labels, IEnumerable<string> skus, string reason, Mark mark = null)
		{
			throw new NotImplementedException();
		}

		public Task RemoveLabelListFromItemListAsync(string[] labels, IEnumerable<string> skus, string reason, Mark mark = null)
		{
			throw new NotImplementedException();
		}
		
		public void AssignLabelListToItemList(string[] labels, bool createLabelIfNotExist, IEnumerable<string> skus, string reason, Mark mark = null)
		{
			throw new NotImplementedException();
		}

		public Task AssignLabelListToItemListAsync(string[] labels, bool createLabelIfNotExist, IEnumerable<string> skus, string reason, Mark mark = null)
		{
			throw new NotImplementedException();
		}

		public void DeleteItem(string sku, Mark mark = null)
		{
			throw new NotImplementedException();
		}

		public Task DeleteItemAsync(string sku, Mark mark = null)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		///	Gets all classifications and their attributes
		/// </summary>
		/// <param name="mark"></param>
		/// <returns></returns>
		public ClassificationConfigurationInformation[] GetClassificationConfigurationInformation( Mark mark = null )
		{
			throw new NotImplementedException();
		}

		/// <summary>
		///	Gets all classifications and their attributes asynchronously
		/// </summary>
		/// <param name="mark"></param>
		/// <returns></returns>
		public Task< ClassificationConfigurationInformation[] > GetClassificationConfigurationInformationAsync( Mark mark = null )
		{
			throw new NotImplementedException();
		}

		public ClassificationConfigurationInformation[] GetClassificationConfigInfo( Mark mark = null )
		{
			throw new NotImplementedException();
		}

		public Task< ClassificationConfigurationInformation[] > GetClassificationConfigInfoAsync( Mark mark = null )
		{
			throw new NotImplementedException();
		}
	}
}
