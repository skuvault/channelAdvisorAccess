﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ChannelAdvisorAccess.Exceptions;
using ChannelAdvisorAccess.REST.Exceptions;
using CuttingEdge.Conditions;
using Polly;

namespace ChannelAdvisorAccess.REST.Shared
{
	public class ActionPolicy
	{
		private readonly int _retryAttempts;

		public ActionPolicy( int attempts )
		{
			Condition.Requires( attempts ).IsGreaterThan( 0 );

			this._retryAttempts = attempts;
		}

		/// <summary>
		///	Retries function until it succeed or failed
		/// </summary>
		/// <typeparam name="TResult"></typeparam>
		/// <param name="funcToThrottle"></param>
		/// <param name="onRetryAttempt">Retry attempts</param>
		/// <param name="extraLogInfo"></param>
		/// <param name="onException"></param>
		/// <returns></returns>
		public Task< TResult > ExecuteAsync< TResult >( Func< Task< TResult > > funcToThrottle, Action< TimeSpan, int > onRetryAttempt )
		{
			return Policy.Handle< ChannelAdvisorNetworkException >()
				.WaitAndRetryAsync( this._retryAttempts,
					retryAttempt => TimeSpan.FromSeconds( Math.Pow( 2, retryAttempt ) ),
					( entityRaw, timeSpan, retryCount, context ) =>
					{
						if ( onRetryAttempt != null )
							onRetryAttempt.Invoke( timeSpan, retryCount );
					})
				.ExecuteAsync( async () =>
				{
					try
					{
						return await funcToThrottle().ConfigureAwait( false );
					}
					catch ( Exception exception )
					{
						Exception caException = exception;

						if ( exception is HttpRequestException
								|| exception is ChannelAdvisorUnauthorizedException )
							caException = new ChannelAdvisorNetworkException( null, exception );
						else
						{
							if ( !( exception is ChannelAdvisorException ) )
								caException = new ChannelAdvisorException( null, exception );
						}

						throw caException;
					}
				});
		}
	}
}
