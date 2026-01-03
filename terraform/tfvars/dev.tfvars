environment = "dev"
location    = "swedencentral"
instance    = "01"

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

api_management_name = "apim-portal-core-dev-swedencentral-01-2db7de738f7a"

geo_location_api = {
  base_url               = "https://apim-geolocation-dev-uksouth-nseckbd66cepc.azure-api.net/geolocation"
  application_audience   = "api://geolocation-api-dev-01"
  keyvault_primary_ref   = "https://kv-rjoqmldvgqxtu-uksouth.vault.azure.net/secrets/portal-repo-func-dev-geolocation-api-key-primary/"
  keyvault_secondary_ref = "https://kv-rjoqmldvgqxtu-uksouth.vault.azure.net/secrets/portal-repo-func-dev-geolocation-api-key-secondary/"
}

repository_api = {
  application_name     = "portal-repository-dev-01"
  application_audience = "api://e56a6947-bb9a-4a6e-846a-1f118d1c3a14/portal-repository-dev-01"
  apim_product_id      = "repository-api"
}

servers_integration_api = {
  application_name     = "portal-servers-integration-dev-01"
  application_audience = "api://portal-servers-integration-dev-01"
  apim_product_id      = "servers-integration-api"
}

tags = {
  Environment = "dev",
  Workload    = "portal-repository-func",
  DeployedBy  = "GitHub-Terraform",
  Git         = "https://github.com/frasermolyneux/portal-repository-func"
}
