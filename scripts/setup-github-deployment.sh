#!/bin/bash

# GitHub Actions Deployment Setup Script
# This script helps set up Azure credentials and GitHub secrets for deployment

set -e

echo "=========================================="
echo "GitHub Actions Deployment Setup"
echo "=========================================="
echo ""

# Check prerequisites
if ! command -v az &> /dev/null; then
    echo "❌ Azure CLI not found. Please install it first:"
    echo "   https://learn.microsoft.com/cli/azure/install-azure-cli"
    exit 1
fi

if ! command -v gh &> /dev/null; then
    echo "❌ GitHub CLI not found. Please install it first:"
    echo "   https://cli.github.com/"
    exit 1
fi

echo "✅ Prerequisites found"
echo ""

# Ask user for deployment option
echo "Select deployment option:"
echo "1. Static Web Apps Direct (frontend only)"
echo "2. Azure Developer CLI with full infrastructure"
echo ""
read -p "Enter choice (1 or 2): " choice

case $choice in
    1)
        echo ""
        echo "Setting up Static Web Apps deployment..."
        echo ""
        
        # Get Static Web App name
        read -p "Enter your Static Web App name (or skip to create manually): " swa_name
        
        if [ -n "$swa_name" ]; then
            # List resource groups
            echo ""
            echo "Available resource groups:"
            az group list --query "[].name" -o tsv
            echo ""
            read -p "Enter resource group name: " rg_name
            
            # Get deployment token
            echo ""
            echo "Fetching Static Web App deployment token..."
            token=$(az staticwebapp secrets list \
                --name "$swa_name" \
                --resource-group "$rg_name" \
                --query "properties.apiKey" -o tsv)
            
            if [ -n "$token" ]; then
                echo "✅ Token retrieved successfully"
                echo ""
                echo "Adding to GitHub secrets..."
                gh secret set AZURE_STATIC_WEB_APPS_API_TOKEN --body "$token"
                echo "✅ Secret added!"
            else
                echo "❌ Failed to get token. Create Static Web App first in Azure Portal"
            fi
        else
            echo "ℹ️  Create Static Web App in Azure Portal:"
            echo "   1. Go to Azure Portal"
            echo "   2. Create → Static Web App"
            echo "   3. Configure for Blazor WebAssembly"
            echo "   4. Get deployment token from Deployment token section"
            echo "   5. Add as GitHub secret: AZURE_STATIC_WEB_APPS_API_TOKEN"
        fi
        ;;
        
    2)
        echo ""
        echo "Setting up Azure Developer CLI deployment..."
        echo ""
        
        # Get subscription info
        echo "Getting Azure subscription info..."
        subscription_id=$(az account show --query id -o tsv)
        tenant_id=$(az account show --query tenantId -o tsv)
        
        echo "Subscription ID: $subscription_id"
        echo "Tenant ID: $tenant_id"
        echo ""
        
        # Create app registration or use existing
        read -p "Enter existing app registration name (or press Enter to create new): " app_name
        
        if [ -z "$app_name" ]; then
            app_name="github-uploaderapp-deployer"
            echo "Creating new app registration: $app_name"
            app=$(az ad app create --display-name "$app_name" --query "appId" -o tsv)
            client_id=$app
            echo "✅ App created with ID: $client_id"
        else
            client_id=$(az ad app list --display-name "$app_name" --query "[0].appId" -o tsv)
            if [ -z "$client_id" ]; then
                echo "❌ App not found. Please create it first."
                exit 1
            fi
            echo "✅ Found app with ID: $client_id"
        fi
        
        echo ""
        echo "Setting up federated credentials..."
        
        # Get repo info
        repo=$(gh repo view --json nameWithOwner -q .nameWithOwner)
        echo "Repository: $repo"
        
        # Create federated credential
        echo "Creating federated credential..."
        az ad app federated-credential create \
            --id "$client_id" \
            --parameters '{"name":"github-deployment","issuer":"https://token.actions.githubusercontent.com","subject":"repo:'"$repo"':ref:refs/heads/main","audiences":["api://AzureADTokenExchange"]}' \
            || echo "ℹ️  Federated credential may already exist"
        
        echo ""
        echo "Assigning Azure RBAC role..."
        az role assignment create \
            --assignee-object-id "$client_id" \
            --role Contributor \
            --scope /subscriptions/"$subscription_id" \
            || echo "ℹ️  Role assignment may already exist"
        
        # Add GitHub secrets
        echo ""
        echo "Adding GitHub secrets..."
        gh secret set AZURE_SUBSCRIPTION_ID --body "$subscription_id"
        gh secret set AZURE_TENANT_ID --body "$tenant_id"
        gh secret set AZURE_CLIENT_ID --body "$client_id"
        
        echo "✅ All secrets added!"
        echo ""
        echo "Environment configuration saved"
        ;;
        
    *)
        echo "Invalid choice"
        exit 1
        ;;
esac

echo ""
echo "=========================================="
echo "✅ Setup complete!"
echo "=========================================="
echo ""
echo "Next steps:"
echo "1. Review .azure/.env configuration"
echo "2. Push changes to your repository"
echo "3. Monitor workflow in Actions tab"
echo ""
