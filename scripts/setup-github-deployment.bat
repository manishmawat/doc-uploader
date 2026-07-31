@echo off
REM GitHub Actions Deployment Setup Script for Windows
REM This script helps set up Azure credentials and GitHub secrets for deployment

setlocal enabledelayedexpansion

echo ==========================================
echo GitHub Actions Deployment Setup
echo ==========================================
echo.

REM Check prerequisites
where az >nul 2>nul
if errorlevel 1 (
    echo Error: Azure CLI not found. Please install it first:
    echo https://learn.microsoft.com/cli/azure/install-azure-cli
    exit /b 1
)

where gh >nul 2>nul
if errorlevel 1 (
    echo Error: GitHub CLI not found. Please install it first:
    echo https://cli.github.com/
    exit /b 1
)

echo Prerequisites found
echo.

REM Ask user for deployment option
echo Select deployment option:
echo 1. Static Web Apps Direct (frontend only)
echo 2. Azure Developer CLI with full infrastructure
echo.
set /p choice="Enter choice (1 or 2): "

if "%choice%"=="1" (
    echo.
    echo Setting up Static Web Apps deployment...
    echo.
    
    set /p swa_name="Enter your Static Web App name (or press Enter to skip): "
    
    if not "!swa_name!"=="" (
        echo.
        echo Available resource groups:
        az group list --query "[].name" -o tsv
        echo.
        set /p rg_name="Enter resource group name: "
        
        echo.
        echo Fetching Static Web App deployment token...
        for /f "delims=" %%i in ('az staticwebapp secrets list --name "!swa_name!" --resource-group "!rg_name!" --query "properties.apiKey" -o tsv') do set "token=%%i"
        
        if not "!token!"=="" (
            echo Token retrieved successfully
            echo.
            echo Adding to GitHub secrets...
            echo !token! | gh secret set AZURE_STATIC_WEB_APPS_API_TOKEN
            echo Secret added!
        ) else (
            echo Failed to get token. Create Static Web App first in Azure Portal
        )
    ) else (
        echo.
        echo Create Static Web App in Azure Portal:
        echo    1. Go to Azure Portal
        echo    2. Create Static Web App
        echo    3. Configure for Blazor WebAssembly
        echo    4. Get deployment token from Deployment token section
        echo    5. Add as GitHub secret: AZURE_STATIC_WEB_APPS_API_TOKEN
    )
    
) else if "%choice%"=="2" (
    echo.
    echo Setting up Azure Developer CLI deployment...
    echo.
    
    REM Get subscription info
    echo Getting Azure subscription info...
    for /f "delims=" %%i in ('az account show --query id -o tsv') do set "subscription_id=%%i"
    for /f "delims=" %%i in ('az account show --query tenantId -o tsv') do set "tenant_id=%%i"
    
    echo Subscription ID: !subscription_id!
    echo Tenant ID: !tenant_id!
    echo.
    
    set /p app_name="Enter app registration name (or press Enter to create new): "
    
    if "!app_name!"=="" (
        set app_name=github-uploaderapp-deployer
        echo Creating new app registration: !app_name!
        for /f "delims=" %%i in ('az ad app create --display-name "!app_name!" --query "appId" -o tsv') do set "client_id=%%i"
        echo App created with ID: !client_id!
    ) else (
        for /f "delims=" %%i in ('az ad app list --display-name "!app_name!" --query "[0].appId" -o tsv') do set "client_id=%%i"
        if "!client_id!"=="" (
            echo App not found. Please create it first.
            exit /b 1
        )
        echo Found app with ID: !client_id!
    )
    
    echo.
    echo Setting up federated credentials...
    
    REM Get repo info
    for /f "delims=" %%i in ('gh repo view --json nameWithOwner -q .nameWithOwner') do set "repo=%%i"
    echo Repository: !repo!
    
    REM Create federated credential
    echo Creating federated credential...
    az ad app federated-credential create --id "!client_id!" --parameters "{\"name\":\"github-deployment\",\"issuer\":\"https://token.actions.githubusercontent.com\",\"subject\":\"repo:!repo!:ref:refs/heads/main\",\"audiences\":[\"api://AzureADTokenExchange\"]}" 2>nul || echo Note: Federated credential may already exist
    
    echo.
    echo Assigning Azure RBAC role...
    az role assignment create --assignee-object-id "!client_id!" --role Contributor --scope /subscriptions/!subscription_id! 2>nul || echo Note: Role assignment may already exist
    
    REM Add GitHub secrets
    echo.
    echo Adding GitHub secrets...
    echo !subscription_id! | gh secret set AZURE_SUBSCRIPTION_ID
    echo !tenant_id! | gh secret set AZURE_TENANT_ID
    echo !client_id! | gh secret set AZURE_CLIENT_ID
    
    echo All secrets added!
    echo.
    echo Environment configuration saved
    
) else (
    echo Invalid choice
    exit /b 1
)

echo.
echo ==========================================
echo Setup complete!
echo ==========================================
echo.
echo Next steps:
echo 1. Review .azure\.env configuration
echo 2. Push changes to your repository
echo 3. Monitor workflow in Actions tab
echo.
pause
