public class InternetConnectionChecker
{
    public async Task<bool> IsInternetConnectedAsync(string healthCheckUrl)
    {
        try
        {
            var handler = new HttpClientHandler
            {
                // Bypass SSL certificate validation (for development purposes only)
                ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true
            };

            using var httpClient = new HttpClient(handler);

            string token = "db8e1d0a-a5c7-4457-b943-9b4f4160a0b1";
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            HttpResponseMessage response = await httpClient.GetAsync(healthCheckUrl);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }
        }
        catch
        {
        }

        return false;
    }
}