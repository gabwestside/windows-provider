criação do exe único  PARA ADM E USUÁRIO:
dotnet publish CredentialProviderAPP.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "c:\credentialprovider\"

-----------------------------------------------

criação do exe do WIN service:

dotnet publish CredentialProviderService\CredentialProviderService.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "C:\CredentialProvider\Service"

powershell pra rodar como adm:

sc.exe create CredentialProviderMFA binPath="C:\CredentialProvider\Service\CredentialProviderService.exe" start=auto DisplayName="Credential Provider MFA Server"

sc.exe description CredentialProviderMFA "Servidor HTTP para autenticacao MFA do Credential Provider"

sc.exe failure CredentialProviderMFA reset=86400 actions=restart/5000/restart/10000/restart/30000


inciiar sem precisar resetar o servidor:
sc.exe start CredentialProviderMFA

verficar se esta rodando:
sc.exe query CredentialProviderMFA

Invoke-RestMethod "http://localhost:5050/health"
Invoke-RestMethod "http://EC2AMAZ-MAL1H1P.aspec.dev:5050/health"