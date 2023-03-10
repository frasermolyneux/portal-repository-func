resource "azurerm_api_management_subscription" "geolocation_api_subscription" {
  provider = azurerm.api_management

  api_management_name = data.azurerm_api_management.platform.name
  resource_group_name = data.azurerm_api_management.platform.resource_group_name

  state = "active"

  api_id       = data.azurerm_api_management_api.geolocation_api.id
  display_name = format("%s-%s", local.function_app_name, data.azurerm_api_management_api.geolocation_api.name)
}

resource "azurerm_api_management_subscription" "repository_api_subscription" {
  provider = azurerm.api_management

  api_management_name = data.azurerm_api_management.platform.name
  resource_group_name = data.azurerm_api_management.platform.resource_group_name

  state = "active"

  api_id       = data.azurerm_api_management_api.repository_api.id
  display_name = format("%s-%s", local.function_app_name, data.azurerm_api_management_api.repository_api.name)
}

resource "azurerm_api_management_subscription" "servers_integration_api_subscription" {
  provider = azurerm.api_management

  api_management_name = data.azurerm_api_management.platform.name
  resource_group_name = data.azurerm_api_management.platform.resource_group_name

  state = "active"

  api_id       = data.azurerm_api_management_api.servers_integration_api.id
  display_name = format("%s-%s", local.function_app_name, data.azurerm_api_management_api.servers_integration_api.name)
}
