environment = "prd"
location    = "uksouth"
instance    = "01"

subscription_id = "32444f38-32f4-409f-889c-8e8aa2b5b4d1"

api_management_name = "apim-portal-core-prd-uksouth-01-f4d9512b0e37"

legacy_api_management_subscription_id     = "903b6685-c12a-4703-ac54-7ec1ff15ca43"
legacy_api_management_resource_group_name = "rg-platform-apim-prd-uksouth-01"
legacy_api_management_name                = "apim-platform-prd-uksouth-ty7og2i6qpv3s"

geo_location_api = {
  base_url               = "https://apim-geolocation-prd-uksouth-cw66ekkwbpohc.azure-api.net"
  application_audience   = "api://geolocation-api-prd-01"
  apim_path_prefix       = "geolocation"
  keyvault_primary_ref   = "https://kv-3b4ntt73fw4ze-uksouth.vault.azure.net/secrets/portal-repo-func-prd-geolocation-api-key-primary/"
  keyvault_secondary_ref = "https://kv-3b4ntt73fw4ze-uksouth.vault.azure.net/secrets/portal-repo-func-prd-geolocation-api-key-secondary/"
}

repository_api = {
  application_name     = "portal-repository-prd-01"
  application_audience = "api://portal-repository-prd-01"
  apim_api_name        = "repository-api"
  apim_api_revision    = "1"
  apim_path_prefix     = "repository"
}

servers_integration_api = {
  application_name     = "portal-servers-integration-prd-01"
  application_audience = "api://portal-servers-integration-prd-01"
  apim_api_name        = "servers-integration-api"
  apim_api_revision    = "1"
  apim_path_prefix     = "servers-integration"
}

tags = {
  Environment = "prd",
  Workload    = "portal",
  DeployedBy  = "GitHub-Terraform",
  Git         = "https://github.com/frasermolyneux/portal-repository-func"
}
