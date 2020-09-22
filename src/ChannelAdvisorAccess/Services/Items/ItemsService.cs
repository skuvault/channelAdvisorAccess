using System;
using System.Runtime.Caching;
using System.Text;
using ChannelAdvisorAccess.Exceptions;
using ChannelAdvisorAccess.InventoryService;
using ChannelAdvisorAccess.Misc;
using Netco.Logging;

namespace ChannelAdvisorAccess.Services.Items
{
	public partial class ItemsService: ServiceBaseAbstr, IItemsService
	{
		private readonly APICredentials _credentials;
		private readonly InventoryServiceSoapClient _client;

		private readonly ObjectCache _cache;
		private readonly string _allItemsCacheKey;
		private readonly object _inventoryCacheLock = new Object();

		public string Name{ get; private set; }
		public string AccountId{ get; private set; }
		public LogDetailsEnum LogDetailsEnum{ get; set; }
		public TimeSpan SlidingCacheExpiration{ get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ItemsService"/> class.
		/// </summary>
		/// <param name="credentials">The credentials.</param>
		/// <param name="name">The account user-friendly name.</param>
		/// <param name="accountId">The account id.</param>
		/// <param name="cache">The cache.</param>
		///  <param name="logDetailsEnum">Log level detail</param>
		/// <remarks>If <paramref name="cache"/> is <c>null</c> no caching takes place.</remarks>
		public ItemsService( APICredentials credentials, string name, string accountId, ObjectCache cache = null, LogDetailsEnum logDetailsEnum = LogDetailsEnum.Undefined )
		{
			this._credentials = credentials;
			this.AccountId = accountId;
			this.LogDetailsEnum = logDetailsEnum;
			this._client = new InventoryServiceSoapClient();

			this.Name = name;
			this._cache = cache;
			this.SlidingCacheExpiration = ObjectCache.NoSlidingExpiration;
			this._allItemsCacheKey = string.Format( "caAllItems_ID_{0}", this.AccountId );
		}

		#region Check for Success
		/// <summary>
		/// Gets the result with success check.
		/// </summary>
		/// <typeparam name="T">Type of the result.</typeparam>
		/// <param name="apiResult">The API result.</param>
		/// <param name="resultData">The result data.</param>
		/// <param name="logError"></param>
		/// <returns>Returns result default value (typically <c>null</c>) if there was a problem
		/// with API call, otherwise returns result.</returns>
		private T GetResultWithSuccessCheck< T >( object apiResult, T resultData, bool logError = true )
		{
			if( !this.IsRequestSuccessful( apiResult, logError ) )
				return default( T );

			return resultData;
		}

		private bool IsRequestSuccessful( object obj, bool logError = true, Mark mark = null )
		{
			var type = obj.GetType();

			var statusProp = type.GetProperty( "Status" );
			var status = ( ResultStatus )statusProp.GetValue( obj, null );

			var messageCodeProp = type.GetProperty( "MessageCode" );
			var messageCode = ( int )messageCodeProp.GetValue( obj, null );

			var message = ( string )type.GetProperty( "Message" ).GetValue( obj, null );

			return this.IsRequestSuccessfulCommon( status, message, messageCode, logError, mark );
		}

		private bool IsRequestSuccessfulImage( object obj )
		{
			GetInventoryItemImageListResponse apiResult = ( GetInventoryItemImageListResponse )obj;

			var status = apiResult.GetInventoryItemImageListResult.Status;
			var message = apiResult.GetInventoryItemImageListResult.Message;
			var messageCode = apiResult.GetInventoryItemImageListResult.MessageCode;

			return this.IsRequestSuccessfulCommon( status, message, messageCode );
		}

		private bool IsRequestSuccessfulAttribute( object obj )
		{
			GetInventoryItemAttributeListResponse apiResult = ( GetInventoryItemAttributeListResponse )obj;

			var status = apiResult.GetInventoryItemAttributeListResult.Status;
			var message = apiResult.GetInventoryItemAttributeListResult.Message;
			var messageCode = apiResult.GetInventoryItemAttributeListResult.MessageCode;

			return this.IsRequestSuccessfulCommon( status, message, messageCode );
		}

		/// <summary>
		/// Determines whether request was successful or not.
		/// </summary>
		/// <param name="status">Result status</param>
		/// <param name="message">Result message</param>
		/// <param name="messageCode">Result message code</param>
		/// <param name="logError">Need to log error message</param>
		/// <param name="mark">Mark</param>
		/// <returns>
		/// 	<c>true</c> if request was successful; otherwise, <c>false</c>.
		/// </returns>
		private bool IsRequestSuccessfulCommon( ResultStatus status, string message, int messageCode, bool logError = true, Mark mark = null )
		{
			var isRequestSuccessful = status == ResultStatus.Success && messageCode == 0;

			if( !isRequestSuccessful && logError )
			{
				var callInfo = new CallInfoBasic( connectionInfo: this.ToJson(), mark: mark, additionalInfo: this.AdditionalLogInfo() );	
				var messageLog = string.Format( "CA Api Request for '{0}' failed with message: {1}", this.AccountId, message );
				ChannelAdvisorLogger.LogTraceFailure( messageLog, callInfo );
			}

			return isRequestSuccessful;
		}

		private static void CheckCaSuccess( APIResultOfBoolean apiResult )
		{
			if( apiResult.Status != ResultStatus.Success )
				throw new ChannelAdvisorException( apiResult.MessageCode, apiResult.Message );
		}

		private static void CheckCaSuccess( APIResultOfArrayOfSynchInventoryItemResponse apiResult )
		{
			if( apiResult.Status != ResultStatus.Success )
			{
				// no sku exists, ignore
				if( apiResult.Message == "All Update requests failed for the SKU list specified!" )
					return;

				var msgs = new StringBuilder();

				foreach( var result in apiResult.ResultData )
				{
					if( !result.Result && !IsSkuMissing( result ) )
						msgs.AppendLine( result.ErrorMessage );
				}

				if( msgs.Length > 0 )
					throw new ChannelAdvisorException( apiResult.MessageCode, string.Concat( apiResult.Message, Environment.NewLine, msgs.ToString() ) );
			}
		}

		private static bool IsSkuMissing( SynchInventoryItemResponse r )
		{
			const string skuMissingMsg = "The specified Sku does not exist";
			return r.ErrorMessage.StartsWith( skuMissingMsg, StringComparison.InvariantCultureIgnoreCase );
		}

		private static void CheckCaSuccess( APIResultOfArrayOfUpdateInventoryItemResponse apiResult )
		{
			if( apiResult.Status != ResultStatus.Success )
			{
				// no sku exists, ignore
				if( apiResult.Message == "All Update requests failed for the SKU list specified!" )
					return;

				if( apiResult.ResultData == null || apiResult.ResultData.Length == 0 )
					throw new ChannelAdvisorException( apiResult.MessageCode, apiResult.Message );

				var msgs = new StringBuilder();

				foreach( var result in apiResult.ResultData )
				{
					if( !result.Result && !IsSkuMissing( result ) )
						msgs.AppendLine( result.ErrorMessage );
				}

				if( msgs.Length > 0 )
					throw new ChannelAdvisorException( apiResult.MessageCode, string.Concat( apiResult.Message, Environment.NewLine, msgs.ToString() ) );
			}
		}

		private static bool IsSkuMissing( UpdateInventoryItemResponse result )
		{
			const string skuMissingMsg = "The specified Sku does not exist";
			return result.ErrorMessage.StartsWith( skuMissingMsg, StringComparison.InvariantCultureIgnoreCase );
		}

		private static void CheckCaSuccess( APIResultOfInt32 result )
		{
			if( result.Status != ResultStatus.Success )
				throw new ChannelAdvisorException( result.MessageCode, result.Message );
		}

		private static void CheckCaSuccess( APIResultOfArrayOfClassificationConfigurationInformation result )
		{
			if( result.Status != ResultStatus.Success )
				throw new ChannelAdvisorException( result.MessageCode, result.Message );
		}

		private void CheckCaSuccess( APIResultOfString result )
		{
			if( result.Status != ResultStatus.Success )
				throw new ChannelAdvisorException( result.MessageCode, result.Message );
		}

		private void CheckCaSuccess( APIResultOfArrayOfDistributionCenterResponse result )
		{
			if( result.Status != ResultStatus.Success )
				throw new ChannelAdvisorException( result.MessageCode, result.Message );
		}
		#endregion
	}
}