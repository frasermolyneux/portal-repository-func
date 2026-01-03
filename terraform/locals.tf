locals {
  # Remote State References
  workload_resource_groups = {
    for location in [var.location] :
    location => data.terraform_remote_state.platform_workloads.outputs.workload_resource_groups[var.workload_name][var.environment].resource_groups[lower(location)]
  }

  workload_backend = try(
    data.terraform_remote_state.platform_workloads.outputs.workload_terraform_backends[var.workload_name][var.environment],
    null
  )

  workload_administrative_unit = try(
    data.terraform_remote_state.platform_workloads.outputs.workload_administrative_units[var.workload_name][var.environment],
    null
  )

  workload_resource_group = local.workload_resource_groups[var.location]

  # Local Resource Naming
  resource_group_name       = "rg-portal-repo-func-${var.environment}-${var.location}-${var.instance}"
  key_vault_name            = "kv-${random_id.environment_id.hex}-${var.location}"
  app_insights_name         = "ai-portal-repo-func-${var.environment}-${var.location}-${var.instance}"
  function_app_name         = "fn-portal-repo-func-${var.environment}-${var.location}-${var.instance}-${random_id.environment_id.hex}"
  function_app_storage_name = "safn${random_id.environment_id.hex}"
}
