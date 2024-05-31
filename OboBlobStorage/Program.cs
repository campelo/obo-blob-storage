using Azure.Core;
using Azure.Storage.Blobs;
using Microsoft.Identity.Client;

string tenantId = "tenant_id";

// FE
string clientIdFE = "fe_id";
string[] scopesFE = ["user.read", "api://be_id/user_impersonation"];

// BE
string clientIdBE = "be_id";
string clientSecretBE = "be_secret";
string[] scopesBE = ["https://storage.azure.com/.default"];

// BLOB
string storageAccountName = "storage_name";
string containerName = "container_name";
string blobName = "blob_name";

string userAccessToken = await GetUserAccessTokenAsync();
Console.WriteLine($"User Access Token: {userAccessToken}");

string oboToken = await GetOboTokenAsync(userAccessToken);
Console.WriteLine($"OBO Token: {oboToken}");

await AccessBlobStorageAsync(oboToken);

async Task<string> GetUserAccessTokenAsync()
{
  var app = PublicClientApplicationBuilder.Create(clientIdFE)
      .WithAuthority(new Uri($"https://login.microsoftonline.com/{tenantId}"))
      .WithRedirectUri("http://localhost")
      .Build();

  var accounts = await app.GetAccountsAsync();
  AuthenticationResult result;

  try
  {
    result = await app.AcquireTokenSilent(scopesFE, accounts.FirstOrDefault())
        .ExecuteAsync();
  }
  catch (MsalUiRequiredException)
  {
    result = await app.AcquireTokenInteractive(scopesFE)
        .ExecuteAsync();
  }

  return result.AccessToken;
}

async Task<string> GetOboTokenAsync(string userAccessToken)
{
  var confidentialClient = ConfidentialClientApplicationBuilder.Create(clientIdBE)
      .WithClientSecret(clientSecretBE)
      .WithAuthority(new Uri($"https://login.microsoftonline.com/{tenantId}"))
      .Build();

  var oboResult = await confidentialClient.AcquireTokenOnBehalfOf(scopesBE, new UserAssertion(userAccessToken))
      .ExecuteAsync();

  return oboResult.AccessToken;
}

async Task AccessBlobStorageAsync(string oboToken)
{
  TokenCredential tokenCredential = new ObTokenCredential(oboToken);
  BlobServiceClient blobServiceClient = new BlobServiceClient(new Uri($"https://{storageAccountName}.blob.core.windows.net"), tokenCredential);
  BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
  BlobClient blobClient = containerClient.GetBlobClient(blobName);

  var response = await blobClient.DownloadAsync();
  using (var stream = response.Value.Content)
  {
    Console.WriteLine("Blob content read successfully.");
  }
}

class ObTokenCredential : TokenCredential
{
  private readonly string _token;

  public ObTokenCredential(string token)
  {
    _token = token;
  }

  public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
  {
    return new AccessToken(_token, DateTimeOffset.MaxValue);
  }

  public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
  {
    return new ValueTask<AccessToken>(new AccessToken(_token, DateTimeOffset.MaxValue));
  }
}