data "azuread_service_principal" "repository_api" {
  display_name = format("portal-repository-%s", var.environment)
}

data "azuread_service_principal" "servers_integration_api" {
  display_name = format("portal-servers-integration-%s", var.environment)
}

data "azuread_service_principal" "geolocation_api" {
  display_name = format("geolocation-lookup-api-%s", var.environment)
}
