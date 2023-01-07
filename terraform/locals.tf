locals {
  resource_group_name       = "rg-portal-repository-func-${var.environment}-${var.location}"
  key_vault_name            = "kv-${random_id.environment_id.hex}-${var.location}"
  app_insights_name         = "ai-ptl-repo-func-${random_id.environment_id.hex}-${var.environment}-${var.location}"
  function_app_name         = "fa-ptl-repo-func-${random_id.environment_id.hex}-${var.environment}-${var.location}"
  function_app_storage_name = "saptlrepofunc${random_id.environment_id.hex}"
}
