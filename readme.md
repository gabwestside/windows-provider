criação do exe único:
dotnet publish CredentialProviderAPP.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "F:\virtualbox\Win-MFA-Lab\MFAAPP"


rodar para testar serviço na maquina controloadora de dominio:
cd c:\credentialprovider\
credentialproviderapp.exe server

verificar se ta rodando:
tasklist /FI "IMAGENAME eq CredentialProviderAPP.exe"
ou
conectando o proprio servico: metodo de healthcheck:
via powershell:

Invoke-RestMethod "http://localhost:5050/health"
Invoke-RestMethod "http://EC2AMAZ-MAL1H1P.aspec.dev:5050/health"

Para matar o serviço:
taskkill /IM CredentialProviderAPP.exe /F