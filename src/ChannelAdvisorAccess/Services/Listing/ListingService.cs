using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChannelAdvisorAccess.Exceptions;
using ChannelAdvisorAccess.ListingService;
using ChannelAdvisorAccess.Misc;
using Netco.Extensions;
using Newtonsoft.Json;

namespace ChannelAdvisorAccess.Services.Listing
{
	public class ListingService: IListingService
	{
		private readonly APICredentials _credentials;
		private readonly ListingServiceSoapClient _client;
		public string Name{ get; private set; }
		public string AccountId{ get; private set; }

		[ JsonIgnore ]
		public Func< string > AdditionalLogInfo{ get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ListingService"/> class.
		/// </summary>
		/// <param name="credentials">The credentials.</param>
		/// <param name="name">The account user-friendly name.</param>
		/// <param name="accountId">The account id.</param>
		public ListingService( APICredentials credentials, string name, string accountId )
		{
			this.Name = name;
			this.AccountId = accountId;
			this._credentials = credentials;
			this._client = new ListingServiceSoapClient();
		}

		#region Ping
		public void Ping()
		{
			AP.CreateQuery( this.AdditionalLogInfo ).Do( () =>
			{
				var result = this._client.Ping( this._credentials );
				this.CheckCaSuccess( result );
			} );
		}

		public async Task PingAsync()
		{
			await AP.CreateQueryAsync( this.AdditionalLogInfo ).Do( async () =>
			{
				var result = await this._client.PingAsync( this._credentials ).ConfigureAwait( false );
				this.CheckCaSuccess( result.PingResult );
			} ).ConfigureAwait( false );
		}
		#endregion

		public void WithdrawListing( IList< string > itemSkus, string withdrawReason )
		{
			if( itemSkus == null || itemSkus.Count == 0 )
				return;

			itemSkus.DoWithPages( 100, s => AP.CreateSubmit( this.AdditionalLogInfo ).Do( () =>
			{
				var result = this._client.WithdrawListings( this._credentials, this.AccountId, s.ToArray(), null, withdrawReason );
				this.CheckCaSuccess( result );
			} ) );
		}

		public async Task WithdrawListingAsync( IList< string > itemSkus, string withdrawReason )
		{
			if( itemSkus == null || itemSkus.Count == 0 )
				return;

			await itemSkus.DoWithPagesAsync( 100, async s => await AP.CreateSubmitAsync( this.AdditionalLogInfo ).Do( async () =>
			{
				var result = await this._client.WithdrawListingsAsync( this._credentials, this.AccountId, s.ToArray(), null, withdrawReason ).ConfigureAwait( false );
				this.CheckCaSuccess( result.WithdrawListingsResult );
			} ).ConfigureAwait( false ) ).ConfigureAwait( false );
		}

		private void CheckCaSuccess( APIResultOfInt32 result )
		{
			if( result.Status != ResultStatus.Success )
				throw new ChannelAdvisorException( result.MessageCode, result.Message );
		}

		private void CheckCaSuccess( APIResultOfString result )
		{
			if( result.Status != ResultStatus.Success )
				throw new ChannelAdvisorException( result.MessageCode, result.Message );
		}
	}
}