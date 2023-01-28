locals {
  resource_group_name       = "rg-portal-repo-func-${var.environment}-${var.location}-${var.instance}"
  key_vault_name            = "kv-${random_id.environment_id.hex}-${var.location}"
  app_insights_name         = "ai-portal-repo-func-${var.environment}-${var.location}-${var.instance}"
  function_app_name         = "fn-portal-repo-func-${var.environment}-${var.location}-${var.instance}-${random_id.environment_id.hex}"
  function_app_storage_name = "safn${random_id.environment_id.hex}"
}
