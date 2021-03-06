﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Backend.Models;
using WalletWasabi.Services;
using WalletWasabi.TorSocks5;
using WalletWasabi.Bases;
using WalletWasabi.Models;
using System.Text;
using NBitcoin.RPC;
using System.Threading;
using WalletWasabi.Backend.Models.Responses;

namespace WalletWasabi.WebClients.Wasabi
{
	public class WasabiClient : TorDisposableBase
	{
		/// <inheritdoc/>
		public WasabiClient(Uri baseUri, IPEndPoint torSocks5EndPoint = null) : base(baseUri, torSocks5EndPoint)
		{
		}

		#region blockchain

		/// <remarks>
		/// Throws OperationCancelledException if <paramref name="cancel"/> is set.
		/// </remarks>
		public async Task<FiltersResponse> GetFiltersAsync(uint256 bestKnownBlockHash, int count, CancellationToken cancel = default)
		{
			using (var response = await TorClient.SendAndRetryAsync(HttpMethod.Get,
																	HttpStatusCode.OK,
																	$"/api/v{Helpers.Constants.BackendMajorVersion}/btc/blockchain/filters?bestKnownBlockHash={bestKnownBlockHash}&count={count}",
																	cancel: cancel))
			{
				if (response.StatusCode == HttpStatusCode.NoContent)
				{
					return null;
				}
				if (response.StatusCode != HttpStatusCode.OK)
				{
					await response.ThrowRequestExceptionFromContentAsync();
				}

				using (HttpContent content = response.Content)
				{
					var ret = await content.ReadAsJsonAsync<FiltersResponse>();
					return ret;
				}
			}
		}

		public async Task<IDictionary<int, FeeEstimationPair>> GetFeesAsync(params int[] confirmationTargets)
		{
			var confirmationTargetsString = string.Join(",", confirmationTargets);

			using (var response = await TorClient.SendAndRetryAsync(HttpMethod.Get, HttpStatusCode.OK, $"/api/v{Helpers.Constants.BackendMajorVersion}/btc/blockchain/fees/{confirmationTargetsString}"))
			{
				if (response.StatusCode != HttpStatusCode.OK)
				{
					await response.ThrowRequestExceptionFromContentAsync();
				}

				using (HttpContent content = response.Content)
				{
					var ret = await content.ReadAsJsonAsync<IDictionary<int, FeeEstimationPair>>();
					return ret;
				}
			}
		}

		public async Task BroadcastAsync(string hex)
		{
			using (var content = new StringContent($"'{hex}'", Encoding.UTF8, "application/json"))
			using (var response = await TorClient.SendAsync(HttpMethod.Post, $"/api/v{Helpers.Constants.BackendMajorVersion}/btc/blockchain/broadcast", content))
			{
				if (response.StatusCode != HttpStatusCode.OK)
				{
					await response.ThrowRequestExceptionFromContentAsync();
				}
			}
		}

		public async Task BroadcastAsync(Transaction transaction)
		{
			await BroadcastAsync(transaction.ToHex());
		}

		public async Task BroadcastAsync(SmartTransaction transaction)
		{
			await BroadcastAsync(transaction.Transaction);
		}

		#endregion blockchain

		#region offchain

		public async Task<IEnumerable<ExchangeRate>> GetExchangeRatesAsync()
		{
			using (var response = await TorClient.SendAndRetryAsync(HttpMethod.Get, HttpStatusCode.OK, $"/api/v{Helpers.Constants.BackendMajorVersion}/btc/offchain/exchange-rates"))
			{
				if (response.StatusCode != HttpStatusCode.OK)
				{
					await response.ThrowRequestExceptionFromContentAsync();
				}

				using (HttpContent content = response.Content)
				{
					var ret = await content.ReadAsJsonAsync<IEnumerable<ExchangeRate>>();
					return ret;
				}
			}
		}

		#endregion offchain

		#region software

		public async Task<(Version ClientVersion, int BackendMajorVersion)> GetVersionsAsync()
		{
			using (var response = await TorClient.SendAndRetryAsync(HttpMethod.Get, HttpStatusCode.OK, "/api/software/versions"))
			{
				if (response.StatusCode == HttpStatusCode.NotFound)
				{
					// Meaning this things wasn't just yet implemented on the running server.
					return (new Version(0, 7), 1);
				}

				if (response.StatusCode != HttpStatusCode.OK)
				{
					await response.ThrowRequestExceptionFromContentAsync();
				}

				using (HttpContent content = response.Content)
				{
					var resp = await content.ReadAsJsonAsync<VersionsResponse>();
					return (Version.Parse(resp.ClientVersion), int.Parse(resp.BackenMajordVersion));
				}
			}
		}

		public async Task<(bool backendCompatible, bool clientUpToDate)> CheckUpdatesAsync()
		{
			var versions = await GetVersionsAsync();
			var clientUpToDate = Helpers.Constants.ClientVersion >= versions.ClientVersion; // If the client version locally is greater or equal to the backend's reported client version, then good.
			var backendCompatible = int.Parse(Helpers.Constants.BackendMajorVersion) == versions.BackendMajorVersion; // If the backend major and the client major equals, then our softwares are compatible.

			return (backendCompatible, clientUpToDate);
		}

		#endregion software
	}
}
