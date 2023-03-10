resource "azurerm_linux_function_app" "app" {
  provider = azurerm.web_apps
  name     = local.function_app_name
  tags     = var.tags

  resource_group_name = data.azurerm_service_plan.plan.resource_group_name
  location            = data.azurerm_service_plan.plan.location
  service_plan_id     = data.azurerm_service_plan.plan.id

  storage_account_name       = azurerm_storage_account.function_app_storage.name
  storage_account_access_key = azurerm_storage_account.function_app_storage.primary_access_key

  https_only = true

  functions_extension_version = "~4"

  identity {
    type = "SystemAssigned"
  }

  site_config {
    application_stack {
      use_dotnet_isolated_runtime = true
      dotnet_version              = "7.0"
    }

    ftps_state          = "Disabled"
    always_on           = true
    minimum_tls_version = "1.2"
  }

  app_settings = {
    "READ_ONLY_MODE"           = var.environment == "prd" ? "true" : "false"
    "WEBSITE_RUN_FROM_PACKAGE" = "1"
    // Fixing Azure Portal issues with Key Vault references: 
    // https://github.com/MicrosoftDocs/azure-docs/issues/66490
    // https://learn.microsoft.com/en-us/azure/azure-monitor/app/sdk-connection-string?tabs=net#is-the-connection-string-a-secret
    //"APPINSIGHTS_INSTRUMENTATIONKEY"             = format("@Microsoft.KeyVault(VaultName=%s;SecretName=%s)", azurerm_key_vault.kv.name, azurerm_key_vault_secret.app_insights_instrumentation_key_secret.name)
    //"APPLICATIONINSIGHTS_CONNECTION_STRING"      = format("@Microsoft.KeyVault(VaultName=%s;SecretName=%s)", azurerm_key_vault.kv.name, azurerm_key_vault_secret.app_insights_connection_string_secret.name)
    "APPINSIGHTS_INSTRUMENTATIONKEY"             = azurerm_application_insights.ai.instrumentation_key
    "APPLICATIONINSIGHTS_CONNECTION_STRING"      = azurerm_application_insights.ai.connection_string
    "ApplicationInsightsAgent_EXTENSION_VERSION" = "~3"
    "apim_base_url"                              = data.azurerm_api_management.platform.gateway_url

    "portal_repository_apim_subscription_key" = format("@Microsoft.KeyVault(VaultName=%s;SecretName=%s)", azurerm_key_vault.kv.name, azurerm_key_vault_secret.repository_api_subscription_secret.name)
    "repository_api_application_audience"     = var.repository_api.application_audience
    "repository_api_path_prefix"              = var.repository_api.apim_path_prefix

    "portal_servers_apim_subscription_key" = format("@Microsoft.KeyVault(VaultName=%s;SecretName=%s)", azurerm_key_vault.kv.name, azurerm_key_vault_secret.servers_integration_api_subscription_secret.name)
    "servers_api_application_audience"     = var.servers_integration_api.application_audience
    "servers_api_path_prefix"              = var.servers_integration_api.apim_path_prefix

    "geolocation_apim_subscription_key"    = format("@Microsoft.KeyVault(VaultName=%s;SecretName=%s)", azurerm_key_vault.kv.name, azurerm_key_vault_secret.geolocation_api_subscription_secret.name)
    "geolocation_api_application_audience" = var.geo_location_api.application_audience
    "geolocation_api_path_prefix"          = var.geo_location_api.apim_path_prefix

    "xtremeidiots_ftp_certificate_thumbprint" = "65173167144EA988088DA20915ABB83DB27645FA"
  }
}
