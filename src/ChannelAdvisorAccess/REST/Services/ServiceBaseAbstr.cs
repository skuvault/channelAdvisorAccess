using ChannelAdvisorAccess.OrderService;
using ChannelAdvisorAccess.Exceptions;
using ChannelAdvisorAccess.Services.Items;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.Threading;
using ChannelAdvisorAccess.Misc;
using ChannelAdvisorAccess.REST.Exceptions;
using ChannelAdvisorAccess.REST.Models.Configuration;
using ChannelAdvisorAccess.REST.Models.Infrastructure;
using ChannelAdvisorAccess.REST.Shared;
using CuttingEdge.Conditions;
using Newtonsoft.Json;
using System.Linq;

namespace ChannelAdvisorAccess.REST.Services
{
	public abstract class RestServiceBaseAbstr : ServiceBaseAbstr
	{
		protected readonly RestCredentials _credentials;
		private readonly APICredentials _soapCredentials;
		private readonly string[] _scope = new string[] { "orders", "inventory" };

		private const int _maxTimeout = 30 * 60 * 1000;
		protected const int _maxConcurrentRequests = 4;
		private const int _minPageSize = 20;
		protected const int _maxBatchSize = 100;
		protected const int _minBatchSize = 20;
		protected int _currentBatchSize = 100;

		protected string _accessToken;
		private DateTime _accessTokenExpiredUtc;
		protected readonly string _refreshToken;
		private static AutoResetEvent _waitHandle = new AutoResetEvent( true );

		protected string AccountName { get; private set; }
		protected HttpClient HttpClient { get; private set; }
		protected readonly ActionPolicy ActionPolicy = new ActionPolicy( 5 );
		protected readonly Throttler Throttler = new Throttler( 4, 1, 10 );
		// 2 000 requests max per minute each batch can include 100 requests (limited to 1500)
		protected readonly Throttler BatchThrottler = new Throttler( 1, 4, 10 );
		protected readonly ChannelAdvisorTimeouts Timeouts;

		private const int _tooManyRequestsStatusCode = 429;

		public string AccountId { get; private set; }
		/// <summary>
		///	Tenant account name to have backward compatibility with existing interface
		/// </summary>
		public string Name {
			get { return this.AccountName; }
		}

		/// <summary>
		///	Rest service for work with orders
		/// </summary>
		/// <param name="credentials">Rest application credentials</param>
		/// <param name="accountName">Tenant account name</param>
		/// <param name="accessToken">Tenant access token</param>
		/// <param name="refreshToken">Tenant refresh token</param>
		protected RestServiceBaseAbstr( RestCredentials credentials, string accountName, string accessToken, string refreshToken, ChannelAdvisorTimeouts timeouts )
		{
			Condition.Requires( credentials ).IsNotNull();
			Condition.Requires( accountName ).IsNotNullOrEmpty();
			Condition.Requires( accessToken ).IsNotNullOrEmpty();
			Condition.Requires( refreshToken ).IsNotNullOrEmpty();

			this._credentials = credentials;
			this.AccountName = accountName;
			this._accessToken = accessToken;
			this._refreshToken = refreshToken;
			this._currentBatchSize = _maxBatchSize;

			this.Timeouts = timeouts;

			this.SetupHttpClient();
			this.LastNetworkActivityTime = DateTime.UtcNow;
		}

		/// <summary>
		///	Rest service with SOAP compatible authentication. Should used only for tenants that already have been granted access to previous SOAP service
		/// </summary>
		/// <param name="credentials">Rest credentials</param>
		/// <param name="soapCredentials">Soap credentials</param>
		/// <param name="accountName">Tenant account name (used for logging)</param>
		/// <param name="accountId">Tenant account id</param>
		protected RestServiceBaseAbstr( RestCredentials credentials, APICredentials soapCredentials, string accountId, string accountName, ChannelAdvisorTimeouts timeouts )
		{
			Condition.Requires( credentials ).IsNotNull();
			Condition.Requires( soapCredentials ).IsNotNull();

			this._credentials = credentials;
			this._soapCredentials = soapCredentials;

			this.AccountId = accountId;
			this.AccountName = accountName;

			this._currentBatchSize = _maxBatchSize;

			this.Timeouts = timeouts;

			this.SetupHttpClient();
			this.LastNetworkActivityTime = DateTime.UtcNow;
		}

		/// <summary>
		///	Init http client used for calling ChannelAdvisor backend
		/// </summary>
		protected void SetupHttpClient()
		{
			this.HttpClient = new HttpClient { BaseAddress = new Uri( ChannelAdvisorEndPoint.BaseApiUrl ) };
			this.HttpClient.Timeout = TimeSpan.FromMilliseconds( _maxTimeout );
			this.HttpClient.DefaultRequestHeaders.Accept.Add( new MediaTypeWithQualityHeaderValue("application/json") );
			this.SetDefaultAuthorizationHeader();
		}

		/// <summary>
		///  Setup HTTP client for REST authorization flow
		/// </summary>
		private void SetDefaultAuthorizationHeader()
		{
			this.HttpClient.DefaultRequestHeaders.Remove( "Authorization" );
			this.HttpClient.DefaultRequestHeaders.Add( "Authorization", String.Format( "Bearer {0}", this._accessToken ) );
		}

		/// <summary>
		///  Setup HTTP client for SOAP compatible authorization flow
		/// </summary>
		private void SetBasicAuthorizationHeader()
		{
			this.HttpClient.DefaultRequestHeaders.Remove( "Authorization" );
			var authHeader = String.Format( "Basic {0}",  Convert.ToBase64String( Encoding.UTF8.GetBytes( this._credentials.ApplicationId + ":" + this._credentials.SharedSecret ) ) );
			this.HttpClient.DefaultRequestHeaders.Add( "Authorization", authHeader );
		}

		/// <summary>
		///	Gets refresh token via developer console credentials
		/// </summary>
		/// <param name="mark">Mark for logging</param>
		/// <returns></returns>
		private Task RefreshAccessToken( CancellationToken token, Mark mark )
		{
			if ( string.IsNullOrEmpty( this._accessToken ) )
				return this.RefreshAccessTokenBySoapCredentials( token, mark, Timeouts[ ChannelAdvisorOperationEnum.RefreshAccessTokenBySoapCredentials ] );
			else
				return this.RefreshAccessTokenByRestCredentials( token, mark, Timeouts[ ChannelAdvisorOperationEnum.RefreshAccessTokenByRestCredentials ] );
		}

		/// <summary>
		///	Gets refresh token by SOAP credentials
		///	This is way how to obtain refresh token using existing credentials without involving partner
		/// </summary>
		/// <param name="mark">Mark for logging</param>
		/// <returns></returns>
		private async Task RefreshAccessTokenBySoapCredentials( CancellationToken token, Mark mark, int? operationTimeout = null )
		{
			_waitHandle.WaitOne();

			this.SetBasicAuthorizationHeader();

			var requestData = new Dictionary< string, string >
			{
				{ "client_id", this._credentials.ApplicationId },
				{ "grant_type", "soap" },
				{ "scope", string.Join( " ", this._scope ) },
				{ "developer_key", this._soapCredentials.DeveloperKey },
				{ "password", this._soapCredentials.Password },
				{ "account_id", this.AccountId }
			};

			var content = new FormUrlEncodedContent( requestData );

			var payloadForLog = new { GrantType = requestData[ "grant_type" ], ClientId = requestData[ "client_id" ], AccountId = requestData[ "account_id" ],
				Scope = requestData[ "scope" ] };
			ChannelAdvisorLogger.LogStarted( this.CreateMethodCallInfo( mark : mark, methodParameters: "oauth2/token", payload: payloadForLog.ToJson(), additionalInfo : this.AdditionalLogInfo(), operationTimeout: operationTimeout ) );

			try
			{
				using( var cts = CancellationTokenSource.CreateLinkedTokenSource( token ) )
				{
					if ( operationTimeout != null )
						cts.CancelAfter( operationTimeout.Value );

					RefreshLastNetworkActivityTime();
					var response = await this.HttpClient.PostAsync( "oauth2/token", content, cts.Token ).ConfigureAwait( false );
					var responseStr = await response.Content.ReadAsStringAsync();
					RefreshLastNetworkActivityTime();
					var result = JsonConvert.DeserializeObject< OAuthResponse >( responseStr );

					if ( !string.IsNullOrEmpty( result.Error ) )
						throw new ChannelAdvisorUnauthorizedException( result.Error );

					this._accessToken = result.AccessToken;
					this._accessTokenExpiredUtc = DateTime.UtcNow.AddSeconds( result.ExpiresIn );

					var resultForLog = new { AccessToken = ChannelAdvisorLogger.SanitizeToken( result.AccessToken ), result.Error, ExpiresOn = this._accessTokenExpiredUtc };
					ChannelAdvisorLogger.LogEnd( this.CreateMethodCallInfo( mark : mark, methodParameters: "oauth2/token", methodResult: resultForLog.ToJson(), additionalInfo : this.AdditionalLogInfo(), operationTimeout: operationTimeout ) );
				}
			}
			catch( Exception ex )
			{
				RefreshLastNetworkActivityTime();
				var channelAdvisorException = new ChannelAdvisorException( ex.Message, ex );
				throw channelAdvisorException;
			}
			finally
			{
				_waitHandle.Set();
				this.SetDefaultAuthorizationHeader();
			}
		}

		/// <summary>
		///	Gets refresh token by REST credentials
		/// </summary>
		/// <param name="mark">Mark for logging</param>
		/// <returns></returns>
		private async Task RefreshAccessTokenByRestCredentials( CancellationToken token, Mark mark, int? operationTimeout = null )
		{
			_waitHandle.WaitOne();
			this.SetBasicAuthorizationHeader();
			AddAccountInfoToHeader();

			var requestData = new Dictionary< string, string > { { "grant_type", "refresh_token" }, { "refresh_token", this._refreshToken } };
			var content = new FormUrlEncodedContent( requestData );
			const string requestTokenUrl = "oauth2/token";

			var payloadForLog = new { GrantType = requestData[ "grant_type" ], RefreshToken = ChannelAdvisorLogger.SanitizeToken( requestData[ "refresh_token" ] ) };
			ChannelAdvisorLogger.LogStarted( this.CreateMethodCallInfo( mark : mark, methodParameters: requestTokenUrl, payload: payloadForLog.ToJson(), additionalInfo : this.AdditionalLogInfo(), operationTimeout: operationTimeout ) );

			try
			{
				using( var cts = CancellationTokenSource.CreateLinkedTokenSource( token ) )
				{
					if ( operationTimeout != null )
						cts.CancelAfter( operationTimeout.Value );

					RefreshLastNetworkActivityTime();
					var response = await this.HttpClient.PostAsync( requestTokenUrl, content, cts.Token ).ConfigureAwait( false );
					var result = new OAuthResponse();
					var responseStr = string.Empty;

					try
					{
						responseStr = await response.Content.ReadAsStringAsync().ConfigureAwait( false );
						RefreshLastNetworkActivityTime();
						result = JsonConvert.DeserializeObject< OAuthResponse >( responseStr );
					}
					catch( Exception responseEx )
					{						
						ChannelAdvisorLogger.LogTrace( this.CreateMethodCallInfo( mark : mark, methodParameters: requestTokenUrl, methodResult: responseStr, errors: "Failed due to" + responseEx.Message, additionalInfo : this.AdditionalLogInfo(), operationTimeout: operationTimeout ) );
						var channelAdvisorException = new ChannelAdvisorException( responseEx.Message, responseEx );
						throw channelAdvisorException;
					}
					
					if ( !string.IsNullOrEmpty( result.Error ) )
						throw new ChannelAdvisorUnauthorizedException( result.Error );

					this._accessToken = result.AccessToken;
					this._accessTokenExpiredUtc = DateTime.UtcNow.AddSeconds( result.ExpiresIn );

					var resultForLog = new { AccessToken = ChannelAdvisorLogger.SanitizeToken( result.AccessToken ), result.Error, ExpiresOn = this._accessTokenExpiredUtc };
					ChannelAdvisorLogger.LogEnd( this.CreateMethodCallInfo( mark : mark, methodParameters: requestTokenUrl, methodResult: resultForLog.ToJson(), additionalInfo : this.AdditionalLogInfo(), operationTimeout: operationTimeout ) );
				}
			}
			catch( Exception ex )
			{
				RefreshLastNetworkActivityTime();
				var channelAdvisorException = new ChannelAdvisorException( ex.Message, ex );
				throw channelAdvisorException;
			}
			finally
			{
				_waitHandle.Set();
				this.SetDefaultAuthorizationHeader();
			}
		}

		private void AddAccountInfoToHeader()
		{
			if( !this.HttpClient.DefaultRequestHeaders.Contains( "account_name" ) )
				this.HttpClient.DefaultRequestHeaders.Add( "account_name", this.AccountName );

			if( !this.HttpClient.DefaultRequestHeaders.Contains( "account_id" ) )
				this.HttpClient.DefaultRequestHeaders.Add( "account_id", this.AccountId );
		}

		/// <summary>
		///	Gets response from REST Endpoint and tries to refresh token if necessary
		/// </summary>
		/// <param name="apiUrl"></param>
		/// <param name="mark"></param>
		/// <param name="collections">Endpoint returns array of objects</param>
		/// <param name="pageNumber">Page ( 0 - all pages )</param>
		/// <param name="pageSize">Page size</param>
		/// <returns></returns>
		protected async Task< PagedApiResponse< T > > GetResponseAsync< T >( string apiUrl, CancellationToken token, Mark mark = null, bool collections = true, int pageNumber = 0, int? pageSize = null, int? operationTimeout = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			var entities = new List< T >();
			var startPage = 1;

			if ( pageNumber > 0 )
				startPage = pageNumber;
			
			var response = await this.GetResponseAsyncByPage< T >( apiUrl, startPage, token, collections, pageSize, mark, operationTimeout ).ConfigureAwait( false );

			if ( response.Value != null )
				entities.AddRange( response.Value );

			var allPagedQueried = response.NextLink == null;

			// check if we have extra pages 
			if ( response.NextLink != null && response.Count != null )
			{
				var totalRecords = response.Count.Value;				
				
				// if specific page was not requested
				if ( pageNumber <= 0 )
				{
					var serviceRecommendedPageSize = this.GetPageSizeFromUrl( response.NextLink );
					var totalPages = ( int )Math.Ceiling( totalRecords * 1.0 / serviceRecommendedPageSize ) + 1;

					var nextPage = 2;
					var options = new ParallelOptions() {  MaxDegreeOfParallelism = 1 };
					Parallel.For( nextPage, totalPages, options, () => new List< T >(), ( currentPage, pls, tempResult ) =>
					{
						var pagedResponse = this.GetResponseAsyncByPage< T >( apiUrl, currentPage, token, false, serviceRecommendedPageSize, mark, operationTimeout ).GetAwaiter().GetResult();
						tempResult.AddRange( pagedResponse.Value );

						return tempResult;
					}, 
					tempResult => {
						lock ( entities )
							entities.AddRange( tempResult );
					});

					allPagedQueried = totalRecords == entities.Count;
				}
				else
				{
					var totalPages = pageSize.HasValue && pageSize.Value != 0 ? ( int )Math.Ceiling( totalRecords * 1.0 / pageSize.Value ) : 1;

					// if we request non existing page CA always returns first page
					if ( pageNumber > totalPages )
					{
						allPagedQueried = true;
						entities.Clear();
					}
				}
			}

			return new PagedApiResponse< T >( entities, startPage, allPagedQueried );
		}

		/// <summary>
		///	Gets response page from REST Endpoint
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="apiUrl"></param>
		/// <param name="requestDataSetSize"></param>
		/// <param name="page"></param>
		/// <param name="pageSize"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		protected Task< ODataResponse< T > > GetResponseAsyncByPage< T >( string apiUrl, int page, CancellationToken token, bool requestDataSetSize = false, int? pageSize = null, Mark mark = null, int? operationTimeout = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			string url = apiUrl;

			if ( !apiUrl.Contains( "?" ) )
			{
				url += "?";
			}

			if ( requestDataSetSize )
				url += "&$count=true";

			if ( pageSize != null && page > 1 )
				url += "&$skip=" + ( page - 1 ) * pageSize;

			return GetEntityAsync< ODataResponse< T > >( url, token, mark, operationTimeout ); 
		}

		/// <summary>
		///	Get entity from REST endpoint
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="apiUrl"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		protected Task< T > GetEntityAsync< T >( string apiUrl, CancellationToken token, Mark mark = null, int? operationTimeout = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			string url = apiUrl;

			return this.Throttler.ExecuteAsync( () => {
				return this.ActionPolicy.ExecuteAsync( async () =>
					{
						using( var cts = CancellationTokenSource.CreateLinkedTokenSource( token ) )
						{
							if ( operationTimeout != null )
								cts.CancelAfter( operationTimeout.Value );

							ChannelAdvisorLogger.LogStarted( this.CreateMethodCallInfo( mark : mark, methodParameters: url, additionalInfo : this.AdditionalLogInfo(), operationTimeout: operationTimeout ) );

							RefreshLastNetworkActivityTime();
							var httpResponse = await this.HttpClient.GetAsync( url, cts.Token ).ConfigureAwait( false );
							var responseStr = await httpResponse.Content.ReadAsStringAsync();
							RefreshLastNetworkActivityTime();

							ChannelAdvisorLogger.LogEnd( this.CreateMethodCallInfo( mark : mark, methodParameters: url, methodResult: responseStr, returnStatusCode: httpResponse.StatusCode.ToString(), additionalInfo : this.AdditionalLogInfo(), operationTimeout: operationTimeout ) );

							await this.ThrowIfError( httpResponse, responseStr, cts.Token, mark ).ConfigureAwait( false );												

							var message = JsonConvert.DeserializeObject< T >( responseStr );

							return message;
						}
					}, 
					( exception, timeSpan, retryAttempt ) => { 
						RefreshLastNetworkActivityTime();
						string retryDetails = this.CreateMethodCallInfo( mark: mark, additionalInfo: this.AdditionalLogInfo(), methodParameters: url );
						ChannelAdvisorLogger.LogTraceRetryStarted( String.Format("Call failed due to {0} trying repeat call {1} time, waited {2} seconds. Details: {3}", exception, retryAttempt, timeSpan.Seconds, retryDetails ) );
					} );
			});
		}

		/// <summary>
		///	Returns binary data from specified url
		/// </summary>
		/// <param name="url"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		protected Task< byte[] > DownloadFile( string url, Mark mark = null )
		{
			return this.HttpClient.GetByteArrayAsync( url );
		}

		/// <summary>
		///	Post data to REST endpoint and handle response
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="apiUrl"></param>
		/// <param name="body"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		protected Task< T > PostAsyncAndGetResult< T >( string apiUrl, string body, CancellationToken token, Mark mark = null, int? operationTimeout = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			string url = apiUrl;

			return this.Throttler.ExecuteAsync( () => {
				return this.ActionPolicy.ExecuteAsync( async () =>
					{
						using( var cts = CancellationTokenSource.CreateLinkedTokenSource( token ) )
						{
							if ( operationTimeout != null )
								cts.CancelAfter( operationTimeout.Value );

							var content = new StringContent( body, Encoding.UTF8, "text/plain" );
							ChannelAdvisorLogger.LogStarted( this.CreateMethodCallInfo( mark : mark, methodParameters: url, payload: body, additionalInfo : this.AdditionalLogInfo(), operationTimeout: operationTimeout ) );
							
							RefreshLastNetworkActivityTime();
							var httpResponse = await HttpClient.PostAsync( apiUrl + "?access_token=" + this._accessToken, content, cts.Token ).ConfigureAwait( false );
							var responseStr = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait( false );
							RefreshLastNetworkActivityTime();

							ChannelAdvisorLogger.LogEnd( this.CreateMethodCallInfo( mark : mark, methodParameters: url, methodResult: responseStr, additionalInfo : this.AdditionalLogInfo(), operationTimeout: operationTimeout ) );

							await this.ThrowIfError( httpResponse, responseStr, cts.Token, mark ).ConfigureAwait( false );

							var message = JsonConvert.DeserializeObject< T >( responseStr );

							return message;
						}
					}, 
					( exception, timeSpan, retryAttempt ) => { 
						RefreshLastNetworkActivityTime();
						string retryDetails = this.CreateMethodCallInfo( mark: mark, additionalInfo: this.AdditionalLogInfo(), methodParameters: url );
						ChannelAdvisorLogger.LogTraceRetryStarted( String.Format("Call failed due to {0} trying repeat call {1} time, waited {2} seconds. Details: {3}", exception, retryAttempt, timeSpan.Seconds, retryDetails ) );
					} );
			});
		}

		/// <summary>
		///	Post data to REST Endpoint
		/// </summary>
		/// <param name="apiUrl"></param>
		/// <param name="data"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		protected Task PostAsync< T >( string apiUrl, T data, CancellationToken token, Mark mark = null, int? operationTimeout = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			return this.Throttler.ExecuteAsync( () => {
				return this.ActionPolicy.ExecuteAsync( async () =>
					{
						using( var cts = CancellationTokenSource.CreateLinkedTokenSource( token ) )
						{
							if ( operationTimeout != null )
								cts.CancelAfter( operationTimeout.Value );

							var payload = JsonConvert.SerializeObject( data );
							var content = new StringContent( payload, Encoding.UTF8, "application/json" );
							ChannelAdvisorLogger.LogStarted( this.CreateMethodCallInfo( mark : mark, methodParameters: apiUrl, payload: payload, additionalInfo : this.AdditionalLogInfo(), operationTimeout: operationTimeout ) );
							
							RefreshLastNetworkActivityTime();
							var httpResponse = await HttpClient.PostAsync( apiUrl + "?access_token=" + this._accessToken, content, cts.Token ).ConfigureAwait( false );
							RefreshLastNetworkActivityTime();
							ChannelAdvisorLogger.LogEnd( this.CreateMethodCallInfo( mark : mark, methodParameters: apiUrl, additionalInfo : this.AdditionalLogInfo(), operationTimeout: operationTimeout ) );
							
							await this.ThrowIfError( httpResponse, null, cts.Token, mark ).ConfigureAwait( false );

							return httpResponse.StatusCode;
						}
					}, 
					( exception, timeSpan, retryAttempt ) => { 
						RefreshLastNetworkActivityTime();
						string retryDetails = this.CreateMethodCallInfo( mark: mark, additionalInfo: this.AdditionalLogInfo(), methodParameters: apiUrl );
						ChannelAdvisorLogger.LogTraceRetryStarted( String.Format("Call failed due to {0} trying repeat call {1} time, waited {2} seconds. Details: {3}", exception, retryAttempt, timeSpan.Seconds, retryDetails ) );
					} );
			});
		}

		/// <summary>
		///	Patch object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="apiUrl"></param>
		/// <param name="data"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		protected Task < HttpStatusCode > PutAsync< T > ( string apiUrl, T data, CancellationToken token, Mark mark = null, int? operationTimeout = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			return this.Throttler.ExecuteAsync( () => {
				return this.ActionPolicy.ExecuteAsync( async () =>
				{
					using( var cts = CancellationTokenSource.CreateLinkedTokenSource( token ) )
					{
						if ( operationTimeout != null )
							cts.CancelAfter( operationTimeout.Value );

						var payload = JsonConvert.SerializeObject( data );
						var content = new StringContent( payload, Encoding.UTF8, "application/json" );
						ChannelAdvisorLogger.LogStarted( this.CreateMethodCallInfo( mark : mark, methodParameters: apiUrl, payload: payload, additionalInfo : this.AdditionalLogInfo(), operationTimeout: operationTimeout ) );
							
						RefreshLastNetworkActivityTime();
						var httpResponse = await HttpClient.PutAsync( apiUrl + "?access_token=" + this._accessToken, content, cts.Token ).ConfigureAwait( false );
						RefreshLastNetworkActivityTime();
						ChannelAdvisorLogger.LogEnd( this.CreateMethodCallInfo( mark : mark, methodParameters: apiUrl, additionalInfo : this.AdditionalLogInfo(), operationTimeout: operationTimeout ) );
							
						await this.ThrowIfError( httpResponse, null, cts.Token, mark ).ConfigureAwait( false );

						return httpResponse.StatusCode;
					}
				}, 
				( exception, timeSpan, retryAttempt ) => { 
					RefreshLastNetworkActivityTime();
					string retryDetails = this.CreateMethodCallInfo( mark: mark, additionalInfo: this.AdditionalLogInfo(), methodParameters: apiUrl );
					ChannelAdvisorLogger.LogTraceRetryStarted( String.Format("Call failed due to {0} trying repeat call {1} time, waited {2} seconds. Details: {3}", exception, retryAttempt, timeSpan.Seconds, retryDetails ) );
				} );
			});
		}

		/// <summary>
		///	Do batch request
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="batch"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		protected async Task < T[] > DoBatch< T >( BatchBuilder batch, CancellationToken token, Mark mark = null, int? operationTimeout = null )
		{
			var result = new List< T >();

			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			var batches = batch.Split( this._currentBatchSize );

			foreach( var batchPart in batches )
			{
				result.AddRange( await DoPartialBatch< T >( batchPart, token, mark, operationTimeout ).ConfigureAwait( false ) );
			}

			return result.ToArray();
		}

		/// <summary>
		///	Do batch request
		/// </summary>
		/// <param name="batch"></param>
		/// <param name="mark"></param>
		/// <returns></returns>
		private Task < T[] > DoPartialBatch< T >( BatchBuilder batch, CancellationToken token, Mark mark = null, int? operationTimeout = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			var url = ChannelAdvisorEndPoint.BatchUrl;

			return this.BatchThrottler.ExecuteAsync( () => {
				return this.ActionPolicy.ExecuteAsync( async () =>
				{
					using( var cts = CancellationTokenSource.CreateLinkedTokenSource( token ) )
					{
						var entities = new List< T >();
						var batches = batch.Split( this._currentBatchSize );

						foreach( var batchPart in batches )
						{
							if ( operationTimeout != null )
								cts.CancelAfter( operationTimeout.Value );

							ChannelAdvisorLogger.LogStarted( this.CreateMethodCallInfo( mark : mark, methodParameters: url, payload: batchPart.ToString(), additionalInfo : this.AdditionalLogInfo(), operationTimeout: operationTimeout ) );
							
							var multipartContent = batchPart.Build();
							RefreshLastNetworkActivityTime();
							var httpResponse = await HttpClient.PostAsync( url + "?access_token=" + this._accessToken, multipartContent, cts.Token ).ConfigureAwait( false );
							string content = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait( false );
							RefreshLastNetworkActivityTime();

							int batchStatusCode = ( int )HttpStatusCode.OK;

							try
							{								
								IEnumerable< T > parsedEntities;
								if ( MultiPartResponseParser.TryParse< T >( content, out batchStatusCode, out parsedEntities ) )
								{
									entities.AddRange( parsedEntities );
								}
								else
								{								
									ChannelAdvisorLogger.LogTrace( this.CreateMethodCallInfo( mark : mark, methodParameters: url, methodResult: content, errors: "Can't parse the response", additionalInfo : this.AdditionalLogInfo(), operationTimeout: operationTimeout ) );
								}
							}
							catch ( Exception ex )
							{								
								ChannelAdvisorLogger.LogTrace( this.CreateMethodCallInfo( mark : mark, methodParameters: url, methodResult: content, errors: "Failed due to: " + ex.Message, additionalInfo : this.AdditionalLogInfo(), operationTimeout: operationTimeout ) );
							}

							if ( ( int )httpResponse.StatusCode == _tooManyRequestsStatusCode )
							{
								// slowly decrease the number of requests in the batch
								if ( this._currentBatchSize >= _minBatchSize )
								{
									this._currentBatchSize = ( int )Math.Ceiling( this._currentBatchSize * 0.7 );
								}
							}

							await this.ThrowIfError( batchStatusCode, content, cts.Token, mark ).ConfigureAwait( false );

							ChannelAdvisorLogger.LogEnd( this.CreateMethodCallInfo( mark : mark, methodParameters: url, methodResult: content.ToJson(), additionalInfo : this.AdditionalLogInfo(), operationTimeout: operationTimeout ) );
						}

						return entities.ToArray();
					}
				}, 
				( exception, timeSpan, retryAttempt ) => { 
					RefreshLastNetworkActivityTime();
					string retryDetails = this.CreateMethodCallInfo( mark: mark, additionalInfo: this.AdditionalLogInfo(), methodParameters: url );
					ChannelAdvisorLogger.LogTraceRetryStarted( String.Format("Call failed due to {0} trying repeat call {1} time, waited {2} seconds. Details: {3}", exception, retryAttempt, timeSpan.Seconds, retryDetails ) );
				} );
			});
		}

		/// <summary>
		///	Validate server response
		/// </summary>
		/// <param name="response"></param>
		/// <param name="message">response message from server</param>
		/// <param name="mark">mark for logging</param>
		private async Task ThrowIfError( HttpResponseMessage response, string message, CancellationToken token, Mark mark )
		{
			if ( message == null )
				message = await response.Content.ReadAsStringAsync().ConfigureAwait( false );

			await ThrowIfError( (int)response.StatusCode, message, token, mark ).ConfigureAwait( false );
		}

		private async Task ThrowIfError( int responseStatusCode, string message, CancellationToken token, Mark mark )
		{
			var isUnauthorized = message.ToUpperInvariant().Contains( "MESSAGE\":\"AUTHORIZATION HAS BEEN DENIED FOR THIS REQUEST.\"" );
			if ( responseStatusCode >= 200 && responseStatusCode < 300 && !isUnauthorized )
				return;

			if ( responseStatusCode == (int)HttpStatusCode.Unauthorized || isUnauthorized )
			{
				// we have to refresh our access token
				await this.RefreshAccessToken( token, mark ).ConfigureAwait( false );

				throw new ChannelAdvisorUnauthorizedException( message );
			}
			else if ( responseStatusCode >= 500
					|| responseStatusCode == _tooManyRequestsStatusCode
					// batch response sometimes contains this code due to factors on ChannelAdvisor side
					|| responseStatusCode == (int)HttpStatusCode.NotAcceptable )
				throw new ChannelAdvisorNetworkException( message );
			
			throw new ChannelAdvisorException( responseStatusCode, message );
		}

		/// <summary>
		///	Convert date in format suitable for REST end point
		/// </summary>
		/// <param name="date"></param>
		/// <returns></returns>
		protected string ConvertDate( DateTime date )
		{
			return date.ToString( "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture );
		}

		/// <summary>
		///	Returns recommended page size by ChannelAdvisor platform for current request
		/// </summary>
		/// <param name="nextLinkUrl"></param>
		/// <returns></returns>
		private int GetPageSizeFromUrl( string nextLinkUrl )
		{
			int pageSize = _minPageSize;

			var query = new Uri( nextLinkUrl ).Query;

			if ( !string.IsNullOrEmpty( query ) )
			{
				string skipParamValue = query.Split( '&' ).Where( pair => pair.IndexOf( "skip" ) > 0 ).FirstOrDefault();

				if ( skipParamValue != null )
					int.TryParse( skipParamValue.Split( '=' )[ 1 ], out pageSize );
			}

			return pageSize;
		}

		/// <summary>
		///	This method is used to update service's last network activity time.
		///	It's called every time before making API request to server or after handling the response.
		/// </summary>
		private void RefreshLastNetworkActivityTime()
		{
			this.LastNetworkActivityTime = DateTime.UtcNow;
		}
	}
}
