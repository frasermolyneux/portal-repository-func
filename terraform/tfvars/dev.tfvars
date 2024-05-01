environment = "dev"
location    = "uksouth"
instance    = "01"

subscription_id = "d68448b0-9947-46d7-8771-baa331a3063a"

api_management_subscription_id     = "d68448b0-9947-46d7-8771-baa331a3063a"
api_management_resource_group_name = "rg-platform-apim-dev-uksouth-01"
api_management_name                = "apim-platform-dev-uksouth-amjx44uuirhb6"

geo_location_api = {
  base_url               = "https://apim-geolocation-dev-uksouth-nseckbd66cepc.azure-api.net"
  application_audience   = "api://geolocation-api-dev-01"
  apim_path_prefix       = "geolocation"
  keyvault_primary_ref   = "https://kv-rjoqmldvgqxtu-uksouth.vault.azure.net/secrets/portal-repo-func-dev-geolocation-api-key-primary/"
  keyvault_secondary_ref = "https://kv-rjoqmldvgqxtu-uksouth.vault.azure.net/secrets/portal-repo-func-dev-geolocation-api-key-secondary/"
}

repository_api = {
  application_name     = "portal-repository-dev-01"
  application_audience = "api://portal-repository-dev-01"
  apim_api_name        = "repository-api"
  apim_api_revision    = "1"
  apim_path_prefix     = "repository"
}

servers_integration_api = {
  application_name     = "portal-servers-integration-dev-01"
  application_audience = "api://portal-servers-integration-dev-01"
  apim_api_name        = "servers-integration-api"
  apim_api_revision    = "1"
  apim_path_prefix     = "servers-integration"
}

tags = {
  Environment = "dev",
  Workload    = "portal",
  DeployedBy  = "GitHub-Terraform",
  Git         = "https://github.com/frasermolyneux/portal-repository-func"
}
