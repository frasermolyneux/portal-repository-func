environment = "prd"
location    = "uksouth"
instance    = "01"

subscription_id = "32444f38-32f4-409f-889c-8e8aa2b5b4d1"

api_management_name = "apim-portal-core-prd-uksouth-01-f4d9512b0e37"

geo_location_api = {
  base_url               = "https://apim-geolocation-prd-uksouth-cw66ekkwbpohc.azure-api.net/geolocation"
  application_audience   = "api://geolocation-api-prd-01"
  keyvault_primary_ref   = "https://kv-3b4ntt73fw4ze-uksouth.vault.azure.net/secrets/portal-repo-func-prd-geolocation-api-key-primary/"
  keyvault_secondary_ref = "https://kv-3b4ntt73fw4ze-uksouth.vault.azure.net/secrets/portal-repo-func-prd-geolocation-api-key-secondary/"
}

repository_api = {
  application_name     = "portal-repository-prd-01"
  application_audience = "api://portal-repository-prd-01"
  apim_product_id      = "repository-api"
}

servers_integration_api = {
  application_name     = "portal-servers-integration-prd-01"
  application_audience = "api://portal-servers-integration-prd-01"
  apim_product_id      = "servers-integration-api"
}

tags = {
  Environment = "prd",
  Workload    = "portal-repository-func",
  DeployedBy  = "GitHub-Terraform",
  Git         = "https://github.com/frasermolyneux/portal-repository-func"
}
