environment = "dev"
location    = "uksouth"
instance    = "01"

subscription_id = "d68448b0-9947-46d7-8771-baa331a3063a"

api_management_name = "apim-portal-core-dev-uksouth-01-3138575b4c87"

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
