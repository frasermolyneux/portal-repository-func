resource "azurerm_key_vault_secret" "app_insights_connection_string_secret" {
  name         = format("%s-connectionstring", azurerm_application_insights.ai.name)
  value        = azurerm_application_insights.ai.connection_string
  key_vault_id = azurerm_key_vault.kv.id

  depends_on = [
    azurerm_role_assignment.deploy_principal_kv_role_assignment
  ]
}

resource "azurerm_key_vault_secret" "app_insights_instrumentation_key_secret" {
  name         = format("%s-instrumentationkey", azurerm_application_insights.ai.name)
  value        = azurerm_application_insights.ai.instrumentation_key
  key_vault_id = azurerm_key_vault.kv.id

  depends_on = [
    azurerm_role_assignment.deploy_principal_kv_role_assignment
  ]
}
