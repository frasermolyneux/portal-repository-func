workload_name = "portal-repository-func"
environment   = "dev"
location      = "swedencentral"

subscription_id = "d68448b0-9947-46d7-8771-baa331a3063a"

platform_workloads_state = {
  resource_group_name  = "rg-tf-platform-workloads-prd-uksouth-01"
  storage_account_name = "sadz9ita659lj9xb3"
  container_name       = "tfstate"
  key                  = "terraform.tfstate"
  subscription_id      = "7760848c-794d-4a19-8cb2-52f71a21ac2b"
  tenant_id            = "e56a6947-bb9a-4a6e-846a-1f118d1c3a14"
}

platform_monitoring_state = {
  resource_group_name  = "rg-tf-platform-monitoring-dev-uksouth-01"
  storage_account_name = "sa9d99036f14d5"
  container_name       = "tfstate"
  key                  = "terraform.tfstate"
  subscription_id      = "7760848c-794d-4a19-8cb2-52f71a21ac2b"
  tenant_id            = "e56a6947-bb9a-4a6e-846a-1f118d1c3a14"
}

portal_environments_state = {
  resource_group_name  = "rg-tf-portal-environments-dev-uksouth-01"
  storage_account_name = "sab36aeb79781b"
  container_name       = "tfstate"
  key                  = "terraform.tfstate"
  subscription_id      = "7760848c-794d-4a19-8cb2-52f71a21ac2b"
  tenant_id            = "e56a6947-bb9a-4a6e-846a-1f118d1c3a14"
}

portal_core_state = {
  resource_group_name  = "rg-tf-portal-core-dev-uksouth-01"
  storage_account_name = "saf39fd6adf871"
  container_name       = "tfstate"
  key                  = "terraform.tfstate"
  subscription_id      = "7760848c-794d-4a19-8cb2-52f71a21ac2b"
  tenant_id            = "e56a6947-bb9a-4a6e-846a-1f118d1c3a14"
}

geo_location_api = {
  base_url               = "https://apim-geo-location-prd-swedencentral-6f10eaac01a0.azure-api.net/geolocation"
  application_audience   = "api://e56a6947-bb9a-4a6e-846a-1f118d1c3a14/geolocation-api-prd"
  keyvault_primary_ref   = "https://kv-2c8ee2a142b1-swe.vault.azure.net/secrets/portal-repository-func-dev-apim-subscription-key/"
  keyvault_secondary_ref = "https://kv-2c8ee2a142b1-swe.vault.azure.net/secrets/portal-repository-func-dev-apim-subscription-key-secondary/"
}

tags = {
  Environment = "dev",
  Workload    = "portal-repository-func",
  DeployedBy  = "GitHub-Terraform",
  Git         = "https://github.com/frasermolyneux/portal-repository-func"
}
