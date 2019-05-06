﻿using ChannelAdvisorAccess.OrderService;
using ChannelAdvisorAccess.Exceptions;
using ChannelAdvisorAccess.Services.Items;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Caching;
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
using System.Diagnostics;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;

namespace ChannelAdvisorAccess.REST.Services
{
	public abstract class RestServiceBaseAbstr : ServiceBaseAbstr
	{
		private readonly RestCredentials _credentials;
		private readonly APICredentials _soapCredentials;
		private readonly string[] _scope = new string[] { "orders", "inventory" };
		private readonly int _requestTimeout = 60 * 1000;
		private readonly int _maxConcurrentRequests = 5;
		private readonly int _minPageSize = 20;
		private string _accessToken;
		private readonly string _refreshToken;

		protected string AccountName { get; private set; }
		protected HttpClient HttpClient { get; private set; }
		protected readonly ActionPolicy ActionPolicy = new ActionPolicy( 3 );
		protected readonly Throttler Throttler = new Throttler( 5, 1, 10 );

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
		protected RestServiceBaseAbstr( RestCredentials credentials, string accountName, string accessToken, string refreshToken )
		{
			Condition.Requires( credentials ).IsNotNull();
			Condition.Requires( accountName ).IsNotNullOrEmpty();
			Condition.Requires( accessToken ).IsNotNullOrEmpty();
			Condition.Requires( refreshToken ).IsNotNullOrEmpty();

			this._credentials = credentials;
			this.AccountName = accountName;
			this._accessToken = accessToken;
			this._refreshToken = refreshToken;

			this.SetupHttpClient();
		}

		/// <summary>
		///	Rest service with SOAP compatible authentication. Should used only for tenants that already have been granted access to previous SOAP service
		/// </summary>
		/// <param name="credentials">Rest credentials</param>
		/// <param name="soapCredentials">Soap credentials</param>
		/// <param name="accountName">Tenant account name (used for logging)</param>
		/// <param name="accountId">Tenant account id</param>
		/// <param name="cache">Cache</param>
		protected RestServiceBaseAbstr( RestCredentials credentials, APICredentials soapCredentials, string accountId, string accountName, ObjectCache cache )
		{
			Condition.Requires( credentials ).IsNotNull();
			Condition.Requires( soapCredentials ).IsNotNull();

			this._credentials = credentials;
			this._soapCredentials = soapCredentials;

			this.AccountId = accountId;
			this.AccountName = accountName;

			this.SetupHttpClient();
		}

		/// <summary>
		///	Init http client used for calling ChannelAdvisor backend
		/// </summary>
		protected void SetupHttpClient()
		{
			this.HttpClient = new HttpClient { BaseAddress = new Uri( ChannelAdvisorEndPoint.BaseApiUrl ) };
			this.HttpClient.DefaultRequestHeaders.Accept.Add( new MediaTypeWithQualityHeaderValue("application/json") );
			this.SetDefaultAuthorizationHeader();
		}

		/// <summary>
		///	Setup cancellation token source where token has predefined operation timeout
		/// </summary>
		protected CancellationToken GetCancellationToken()
		{
			var cancellationTokenSource = new CancellationTokenSource();
			cancellationTokenSource.CancelAfter( this._requestTimeout );

			return cancellationTokenSource.Token;
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
		/// <returns></returns>
		private Task RefreshAccessToken()
		{
			if ( string.IsNullOrEmpty( this._accessToken ) )
				return this.RefreshAccessTokenBySoapCredentials();
			else
				return this.RefreshAccessTokenByRestCredentials();
		}

		/// <summary>
		///	Gets refresh token by SOAP credentials
		///	This is way how to obtain refresh token using existing credentials without involving partner
		/// </summary>
		/// <returns></returns>
		private async Task RefreshAccessTokenBySoapCredentials()
		{
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

			try
			{
				var response = await this.HttpClient.PostAsync( "oauth2/token", content ).ConfigureAwait( false );
				var responseStr = await response.Content.ReadAsStringAsync();
				var result = JsonConvert.DeserializeObject< OAuthResponse >( responseStr );

				if ( !string.IsNullOrEmpty( result.Error ) )
					throw new ChannelAdvisorException( result.Error );

				this._accessToken = result.AccessToken;
			}
			catch( Exception ex )
			{
				var channelAdvisorException = new ChannelAdvisorException( ex.Message, ex );
				throw channelAdvisorException;
			}
			finally
			{
				this.SetDefaultAuthorizationHeader();
			}
		}

		/// <summary>
		///	Gets refresh token by REST credentials
		/// </summary>
		/// <returns></returns>
		private async Task RefreshAccessTokenByRestCredentials()
		{
			this.SetBasicAuthorizationHeader();

			var requestData = new Dictionary< string, string > { { "grant_type", "refresh_token" }, { "refresh_token", this._refreshToken } };
			var content = new FormUrlEncodedContent( requestData );

			try
			{
				var response = await this.HttpClient.PostAsync( "oauth2/token", content ).ConfigureAwait( false );
				var responseStr = await response.Content.ReadAsStringAsync();
				var result = JsonConvert.DeserializeObject< OAuthResponse >( responseStr );

				if ( !string.IsNullOrEmpty( result.Error ) )
					throw new ChannelAdvisorException( result.Error );

				this._accessToken = result.AccessToken;
			}
			catch( Exception ex )
			{
				var channelAdvisorException = new ChannelAdvisorException( ex.Message, ex );
				throw channelAdvisorException;
			}
			finally
			{
				this.SetDefaultAuthorizationHeader();
			}
		}

		/// <summary>
		///	Gets response from REST Endpoint and tries to refresh token if necessary
		/// </summary>
		/// <param name="apiUrl"></param>
		/// <param name="mark"></param>
		/// <param name="collections">Endpoint returns array of objects</param>
		/// <returns></returns>
		protected async Task< IEnumerable< T > > GetResponseAsync< T >( string apiUrl, Mark mark = null, bool collections = true )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			var entities = new List< T >();
			
			var response = await this.GetResponseAsyncByPage< T >( apiUrl, 1, collections, null, mark ).ConfigureAwait( false );

			if ( response.Value != null )
				entities.AddRange( response.Value );

			// check if we have extra pages
			if ( response.NextLink != null && response.Count != null )
			{
				var totalRecords = response.Count.Value;
				var pageSize = this.GetPageSizeFromUrl( response.NextLink );
				var startPage = 2;
				var pages = (int)Math.Ceiling( totalRecords * 1.0 / pageSize ) + 1; 
				var options = new ParallelOptions() {  MaxDegreeOfParallelism = this._maxConcurrentRequests };
				
				Parallel.For( startPage, pages, options, () => new List< T >(), ( page, pls, tempResult ) =>
				{
					var pagedResponse = this.GetResponseAsyncByPage< T >( apiUrl, page, false, pageSize, mark ).GetAwaiter().GetResult();
					tempResult.AddRange( pagedResponse.Value );

					return tempResult;
				}, 
				tempResult => {
					lock ( entities )
						entities.AddRange( tempResult );
				});
			}

			return entities;
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
		protected Task< ODataResponse< T > > GetResponseAsyncByPage< T >( string apiUrl, int page, bool requestDataSetSize = false, int? pageSize = null, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			string url = apiUrl;

			if ( requestDataSetSize )
				url += ( apiUrl.Contains("?") ? "&" : "?" ) + "$count=true";

			if ( pageSize != null && page != 1 )
				url += "&$skip=" + ( page - 1 ) * pageSize;

			return this.Throttler.ExecuteAsync( () => {
				return this.ActionPolicy.ExecuteAsync( async () =>
					{
						var httpResponse = await this.HttpClient.GetAsync( url, this.GetCancellationToken() ).ConfigureAwait( false );
						var responseStr = await httpResponse.Content.ReadAsStringAsync();

						await this.ThrowIfError( httpResponse, responseStr ).ConfigureAwait( false );
					
						var message = JsonConvert.DeserializeObject< ODataResponse< T > >( responseStr );

						return message;
					}, 
					( timeSpan, retryAttempt ) => { 
						string retryDetails = this.CreateMethodCallInfo( mark: mark, additionalInfo: this.AdditionalLogInfo(), methodParameters: url );
					
						ChannelAdvisorLogger.LogTraceRetryStarted( String.Format("Call failed, trying repeat call {0} time, waited {1} seconds. Details: {2}", retryAttempt, timeSpan.Seconds, retryDetails ) );
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
		protected Task PostAsync< T >( string apiUrl, T data, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			return this.Throttler.ExecuteAsync( () => {
				return this.ActionPolicy.ExecuteAsync( async () =>
					{
						var content = new StringContent( JsonConvert.SerializeObject( data ), Encoding.UTF8, "application/json" );
						var httpResponse = await HttpClient.PostAsync( apiUrl + "?access_token=" + this._accessToken, content, this.GetCancellationToken() ).ConfigureAwait( false );

						await this.ThrowIfError( httpResponse, null ).ConfigureAwait( false );

						return httpResponse.StatusCode;
					}, 
					( timeSpan, retryAttempt ) => { 
						string retryDetails = this.CreateMethodCallInfo( mark: mark, additionalInfo: this.AdditionalLogInfo(), methodParameters: apiUrl );
					
						ChannelAdvisorLogger.LogTraceRetryStarted( String.Format("Call failed, trying repeat call {0} time, waited {1} seconds. Details: {2}", retryAttempt, timeSpan.Seconds, retryDetails ) );
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
		protected Task < HttpStatusCode > PutAsync< T > ( string apiUrl, T data, Mark mark = null )
		{
			if( mark.IsBlank() )
				mark = Mark.CreateNew();

			return this.Throttler.ExecuteAsync( () => {
				return this.ActionPolicy.ExecuteAsync( async () =>
				{
					var content = new StringContent( JsonConvert.SerializeObject( data ), Encoding.UTF8, "application/json" );
					var httpResponse = await this.HttpClient.PutAsync( apiUrl + "?access_token=" + this._accessToken, content, this.GetCancellationToken() );

					await this.ThrowIfError( httpResponse, null ).ConfigureAwait( false );

					return httpResponse.StatusCode;
				}, 
				( timeSpan, retryAttempt ) => { 
					var retryDetails = this.CreateMethodCallInfo( mark: mark, additionalInfo: this.AdditionalLogInfo(), methodParameters: apiUrl );
					
					ChannelAdvisorLogger.LogTraceRetryStarted( String.Format("Call failed, trying repeat call {0} time, waited {1} seconds. Details: {2}", retryAttempt, timeSpan.Seconds, retryDetails ) );
				} );
			});
		}

		/// <summary>
		///	Validate server response
		/// </summary>
		/// <param name="response"></param>
		/// <param name="message">response message from server</param>
		private async Task ThrowIfError( HttpResponseMessage response, string message )
		{
			if ( response.IsSuccessStatusCode )
				return;

			if ( message == null )
				message = await response.Content.ReadAsStringAsync();

			if ( response.StatusCode == HttpStatusCode.Unauthorized )
			{
				// we have to refresh our access token
				await this.RefreshAccessToken().ConfigureAwait( false );
				
				throw new ChannelAdvisorUnauthorizedException( message );
			}
			else if ( response.StatusCode == HttpStatusCode.ServiceUnavailable )
				throw new ChannelAdvisorNetworkException( message );
			
			throw new ChannelAdvisorException( (int)response.StatusCode, message );
		}

		/// <summary>
		///	Convert date in format suitable for REST end point
		/// </summary>
		/// <param name="date"></param>
		/// <returns></returns>
		protected string ConvertDate( DateTime date )
		{
			return date.ToString( "yyyy-MM-ddThh:mm:ssZ", CultureInfo.InvariantCulture );
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
				string skipParamValue = query.Split('&').Where( pair => pair.IndexOf("skip") > 0 ).FirstOrDefault();

				if ( skipParamValue != null )
					int.TryParse( skipParamValue.Split('=')[1], out pageSize );
			}

			return pageSize;
		}
	}
}