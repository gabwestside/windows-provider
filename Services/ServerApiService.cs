using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CredentialProviderAPP.Models.Api;
using CredentialProviderAPP.Utils;

namespace CredentialProviderAPP.Services
{
    public static class ServerApiService
    {
        private static HttpClient CreateHttpClient()
        {
            string baseUrl = ConfigHelper.Get("Server:BaseUrl");

            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new InvalidOperationException("A configuração Server:BaseUrl não foi definida.");

            return new HttpClient
            {
                BaseAddress = new Uri(baseUrl, UriKind.Absolute),
                Timeout = TimeSpan.FromSeconds(20)
            };
        }

        public static async Task<MfaSetupResponse> ObterSetupMfaAsync(string login)
        {
            using var httpClient = CreateHttpClient();

            var response = await httpClient.GetAsync($"mfa/setup?login={Uri.EscapeDataString(login)}");

            var result = await response.Content.ReadFromJsonAsync<MfaSetupResponse>();
            return result ?? new MfaSetupResponse
            {
                Sucesso = false,
                Erro = $"Erro HTTP {(int)response.StatusCode}"
            };
        }

        public static async Task<MfaStatusResponse> ObterStatusMfaAsync(string login)
        {
            using var httpClient = CreateHttpClient();

            var response = await httpClient.GetAsync($"mfa/status?login={Uri.EscapeDataString(login)}");

            var result = await response.Content.ReadFromJsonAsync<MfaStatusResponse>();
            return result ?? new MfaStatusResponse
            {
                Sucesso = false,
                Erro = $"Erro HTTP {(int)response.StatusCode}"
            };
        }

        public static async Task<ValidateMfaResponse> ValidarCodigoMfaAsync(string login, string codigo)
        {
            using var httpClient = CreateHttpClient();

            var request = new ValidateMfaRequest
            {
                Login = login,
                Codigo = codigo
            };

            var response = await httpClient.PostAsJsonAsync("mfa/validate", request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ValidateMfaResponse>();
            return result ?? new ValidateMfaResponse
            {
                Sucesso = false,
                Erro = "Resposta inválida do servidor."
            };
        }

        public static async Task<DefaultApiResponse> TrocarSenhaAsync(string login, string novaSenha)
        {
            using var httpClient = CreateHttpClient();

            var request = new ChangePasswordRequest
            {
                Login = login,
                NovaSenha = novaSenha
            };

            var response = await httpClient.PostAsJsonAsync("password/change", request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<DefaultApiResponse>();
            return result ?? new DefaultApiResponse
            {
                Sucesso = false,
                Erro = "Resposta inválida do servidor."
            };
        }

        public static async Task<ValidatePasswordResponse> ValidarSenhaAsync(string login, string novaSenha)
        {
            using var httpClient = CreateHttpClient();

            var request = new ChangePasswordRequest
            {
                Login = login,
                NovaSenha = novaSenha
            };

            var response = await httpClient.PostAsJsonAsync("password/validate-candidate", request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ValidatePasswordResponse>();
            return result ?? new ValidatePasswordResponse
            {
                Sucesso = false,
                Erro = "Resposta inválida do servidor."
            };
        }
    }
}