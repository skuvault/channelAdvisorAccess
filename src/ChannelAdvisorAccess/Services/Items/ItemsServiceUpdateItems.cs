using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChannelAdvisorAccess.InventoryService;
using ChannelAdvisorAccess.Misc;
using CuttingEdge.Conditions;
using Netco.Extensions;

namespace ChannelAdvisorAccess.Services.Items
{
	public partial class ItemsService: IItemsService
	{
		#region Update items
		public void SynchItem( InventoryItemSubmit item, bool isCreateNew = false, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			var parameters = new { item, isCreateNew };

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : parameters.ToJson() ) );

				AP.CreateSubmit( this.AdditionalLogInfo, mark ).Do( () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : parameters.ToJson() ) );
					if( !isCreateNew && !this.DoesSkuExist( item.Sku, mark ) )
						return;

					var resultOfBoolean = this._client.SynchInventoryItem( this._credentials, this.AccountId, item );
					CheckCaSuccess( resultOfBoolean );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : resultOfBoolean.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : parameters.ToJson() ) );
				} );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : "void", additionalInfo : this.AdditionalLogInfo(), methodParameters : parameters.ToJson() ) );
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public async Task SynchItemAsync( InventoryItemSubmit item, bool isCreateNew = false, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			var parameters = new { item, isCreateNew };

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : parameters.ToJson() ) );

				await AP.CreateSubmitAsync( this.AdditionalLogInfo, mark ).Do( async () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : parameters.ToJson() ) );
					if( !isCreateNew && !( await this.DoesSkuExistAsync( item.Sku, mark ).ConfigureAwait( false ) ) )
						return;

					var resultOfBoolean = await this._client.SynchInventoryItemAsync( this._credentials, this.AccountId, item ).ConfigureAwait( false );
					CheckCaSuccess( resultOfBoolean.SynchInventoryItemResult );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : resultOfBoolean.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : parameters.ToJson() ) );
				} ).ConfigureAwait( false );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : "void", additionalInfo : this.AdditionalLogInfo(), methodParameters : parameters.ToJson() ) );
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public void SynchItems( IEnumerable< InventoryItemSubmit > items, bool isCreateNew = false, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			var parameters = new { items, isCreateNew };

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : parameters.ToJson() ) );

				if( !isCreateNew )
				{
					var existSkus = this.DoSkusExist( items.Select( x => x.Sku ), mark ).Select( x => x.Sku );
					items = items.Where( x => existSkus.Contains( x.Sku ) );
				}

				var itemsByPages = ToChunks( items, 100 );
				foreach( var i in itemsByPages )
				{
					AP.CreateSubmit( this.AdditionalLogInfo, mark ).Do( () =>
					{
						ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : i.ToJson() ) );
						var resultOfBoolean = this._client.SynchInventoryItemList( this._credentials, this.AccountId, i.ToArray() );
						CheckCaSuccess( resultOfBoolean );
						ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : resultOfBoolean.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : i.ToJson() ) );
					} );
				}
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : "void", additionalInfo : this.AdditionalLogInfo(), methodParameters : parameters.ToJson() ) );
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public async Task SynchItemsAsync( IEnumerable< InventoryItemSubmit > items, bool isCreateNew = false, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			var parameters = new { items, isCreateNew };

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : parameters.ToJson() ) );

				if( !isCreateNew )
				{
					var existSkus = ( this.DoSkusExist( items.Select( x => x.Sku ), mark ) ).Select( x => x.Sku );
					items = items.Where( x => existSkus.Contains( x.Sku ) );
				}

				await items.DoWithPagesAsync( 100, async i => await AP.CreateSubmitAsync( this.AdditionalLogInfo, mark ).Do( async () =>
				{
					await AP.CreateSubmitAsync( this.AdditionalLogInfo, mark ).Do( async () =>
					{
						ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : i.ToJson() ) );
						var resultOfBoolean = await this._client.SynchInventoryItemListAsync( this._credentials, this.AccountId, i.ToArray() ).ConfigureAwait( false );
						CheckCaSuccess( resultOfBoolean.SynchInventoryItemListResult );
						ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : resultOfBoolean.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : i.ToJson() ) );
					} ).ConfigureAwait( false );
				} ).ConfigureAwait( false ) ).ConfigureAwait( false );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : "void", additionalInfo : this.AdditionalLogInfo(), methodParameters : parameters.ToJson() ) );
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public void UpdateQuantityAndPrice( InventoryItemQuantityAndPrice itemQuantityAndPrice, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : itemQuantityAndPrice.ToJson() ) );

				AP.CreateSubmit( this.AdditionalLogInfo, mark ).Do( () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : itemQuantityAndPrice.ToJson() ) );
					var resultOfBoolean = this._client.UpdateInventoryItemQuantityAndPrice( this._credentials, this.AccountId, itemQuantityAndPrice );
					CheckCaSuccess( resultOfBoolean );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : resultOfBoolean.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : itemQuantityAndPrice.ToJson() ) );
				} );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : "void", additionalInfo : this.AdditionalLogInfo(), methodParameters : itemQuantityAndPrice.ToJson() ) );
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public async Task UpdateQuantityAndPriceAsync( InventoryItemQuantityAndPrice itemQuantityAndPrice, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : itemQuantityAndPrice.ToJson() ) );

				await AP.CreateSubmitAsync( this.AdditionalLogInfo, mark ).Do( async () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : itemQuantityAndPrice.ToJson() ) );
					var resultOfBoolean = await this._client.UpdateInventoryItemQuantityAndPriceAsync( this._credentials, this.AccountId, itemQuantityAndPrice ).ConfigureAwait( false );
					CheckCaSuccess( resultOfBoolean.UpdateInventoryItemQuantityAndPriceResult );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : resultOfBoolean.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : itemQuantityAndPrice.ToJson() ) );
				} ).ConfigureAwait( false );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : "void", additionalInfo : this.AdditionalLogInfo(), methodParameters : itemQuantityAndPrice.ToJson() ) );
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public void UpdateQuantityAndPrices( IEnumerable< InventoryItemQuantityAndPrice > itemQuantityAndPrices, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : itemQuantityAndPrices.ToJson() ) );

				var itemQuantityAndPricesByPages = ToChunks( itemQuantityAndPrices, 500 );
				foreach( var itemsPage in itemQuantityAndPricesByPages )
				{
					AP.CreateSubmit( this.AdditionalLogInfo, mark ).Do( () =>
					{
						ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : itemsPage.ToJson() ) );
						var resultOfBoolean = this._client.UpdateInventoryItemQuantityAndPriceList( this._credentials, this.AccountId, itemsPage.ToArray() );
						CheckCaSuccess( resultOfBoolean );
						ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : resultOfBoolean.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : itemsPage.ToJson() ) );
					} );
				}
				
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : "void", additionalInfo : this.AdditionalLogInfo(), methodParameters : itemQuantityAndPrices.ToJson() ) );
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public async Task UpdateQuantityAndPricesAsync( IEnumerable< InventoryItemQuantityAndPrice > itemQuantityAndPrices, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : itemQuantityAndPrices.ToJson() ) );

				var itemQuantityAndPricesByPages = ToChunks( itemQuantityAndPrices, 800 );
				await itemQuantityAndPricesByPages.DoInBatchAsync( 3, async itemsPage =>
				{
					await AP.CreateSubmitAsync( this.AdditionalLogInfo, mark ).Do( async () =>
					{
						ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : itemsPage.ToJson() ) );
						var result = await this._client.UpdateInventoryItemQuantityAndPriceListAsync( this._credentials, this.AccountId, itemsPage.ToArray() ).ConfigureAwait( false );
						CheckCaSuccess( result.UpdateInventoryItemQuantityAndPriceListResult );
						ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : result.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : itemsPage.ToJson() ) );
					} ).ConfigureAwait( false );
				} ).ConfigureAwait( false );
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public void RemoveLabelListFromItemList( string[] labels, IEnumerable< string > skus, string reason, Mark mark = null )
		{
			Condition.Requires( labels, "labels" ).IsShorterOrEqual( 3, "Only up to 3 labels allowed." ).IsNotNull();

			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			var parameters = new { labels, skus, reason };

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : parameters.ToJson() ) );

				var skusByPages = ToChunks( skus, 500 );
				foreach( var s in skusByPages )
				{
					AP.CreateSubmit( this.AdditionalLogInfo, mark ).Do( () =>
					{
						ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : s.ToJson() ) );
						var resultOfBoolean = this._client.RemoveLabelListFromInventoryItemList( this._credentials, this.AccountId, labels, s.ToArray(), reason );
						CheckCaSuccess( resultOfBoolean );
						ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : resultOfBoolean.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : s.ToJson() ) );
					} );
				}

				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : parameters.ToJson() ) );
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public async Task RemoveLabelListFromItemListAsync( string[] labels, IEnumerable< string > skus, string reason, Mark mark = null )
		{
			Condition.Requires( labels, "labels" ).IsShorterOrEqual( 3, "Only up to 3 labels allowed." ).IsNotNull();

			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			var parameters = new { labels, skus, reason };

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : parameters.ToJson() ) );

				await skus.DoWithPagesAsync( 500, async s => await AP.CreateSubmitAsync( this.AdditionalLogInfo, mark ).Do( async () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : s.ToJson() ) );
					var resultOfBoolean = await this._client.RemoveLabelListFromInventoryItemListAsync( this._credentials, this.AccountId, labels, s.ToArray(), reason ).ConfigureAwait( false );
					CheckCaSuccess( resultOfBoolean.RemoveLabelListFromInventoryItemListResult );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : resultOfBoolean.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : s.ToJson() ) );
				} ).ConfigureAwait( false ) ).ConfigureAwait( false );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : parameters.ToJson() ) );
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public void AssignLabelListToItemList( string[] labels, bool createLabelIfNotExist, IEnumerable< string > skus, string reason, Mark mark = null )
		{
			Condition.Requires( labels, "labels" ).IsShorterOrEqual( 3, "Only up to 3 labels allowed." ).IsNotNull();

			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			var parameters = new { labels, createLabelIfNotExist, skus, reason };
			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : parameters.ToJson() ) );

				var skusByPages = ToChunks( skus, 500 );
				foreach( var s in skusByPages )
				{
					AP.CreateSubmit( this.AdditionalLogInfo, mark ).Do( () =>
					{
						ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : s.ToJson() ) );
						var resultOfBoolean = this._client.AssignLabelListToInventoryItemList( this._credentials, this.AccountId, labels, createLabelIfNotExist, s.ToArray(), reason );
						CheckCaSuccess( resultOfBoolean );
						ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : resultOfBoolean.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : s.ToJson() ) );
					} );
				}
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : parameters.ToJson() ) );
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}

		public async Task AssignLabelListToItemListAsync( string[] labels, bool createLabelIfNotExist, IEnumerable< string > skus, string reason, Mark mark = null )
		{
			Condition.Requires( labels, "labels" ).IsShorterOrEqual( 3, "Only up to 3 labels allowed." ).IsNotNull();

			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			var parameters = new { labels, createLabelIfNotExist, skus, reason };

			try
			{
				ChannelAdvisorLogger.LogStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : parameters.ToJson() ) );

				await skus.DoWithPagesAsync( 500, async s => await AP.CreateSubmitAsync( this.AdditionalLogInfo, mark ).Do( async () =>
				{
					ChannelAdvisorLogger.LogTraceRetryStarted( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : s.ToJson() ) );
					var resultOfBoolean = await this._client.AssignLabelListToInventoryItemListAsync( this._credentials, this.AccountId, labels, createLabelIfNotExist, s.ToArray(), reason ).ConfigureAwait( false );
					CheckCaSuccess( resultOfBoolean.AssignLabelListToInventoryItemListResult );
					ChannelAdvisorLogger.LogTraceRetryEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, methodResult : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : resultOfBoolean.ToJson(), additionalInfo : this.AdditionalLogInfo(), methodParameters : !this.LogDetailsEnum.HasFlag( LogDetailsEnum.LogParametersAndResultForRetry ) ? null : s.ToJson() ) );
				} ).ConfigureAwait( false ) ).ConfigureAwait( false );
				ChannelAdvisorLogger.LogEnd( new CallInfo( connectionInfo: this.ToJson(), mark : mark, additionalInfo : this.AdditionalLogInfo(), methodParameters : parameters.ToJson() ) );
			}
			catch( Exception exception )
			{
				throw this.HandleExceptionAndLog( mark, exception );
			}
		}
		#endregion}
	}
}