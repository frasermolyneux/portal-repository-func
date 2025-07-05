variable "environment" {
  default = "dev"
}

variable "location" {
  default = "uksouth"
}

variable "instance" {
  default = "01"
}

variable "subscription_id" {}

variable "api_management_name" {}

variable "geo_location_api" {
  type = object({
    base_url               = string
    application_audience   = string
    apim_path_prefix       = string
    keyvault_primary_ref   = string
    keyvault_secondary_ref = string
  })
}

variable "repository_api" {
  type = object({
    application_name     = string
    application_audience = string
    apim_product_id      = string
    apim_path_prefix     = string
  })
  default = {
    application_name     = "portal-repository-dev-01"
    application_audience = "api://portal-repository-dev-01"
    apim_product_id      = ""
    apim_path_prefix     = "repository"
  }
}

variable "servers_integration_api" {
  type = object({
    application_name     = string
    application_audience = string
    apim_product_id      = string
  })
  default = {
    application_name     = "portal-servers-integration-dev-01"
    application_audience = "api://portal-servers-integration-dev-01"
    apim_product_id      = ""
  }
}

variable "tags" {
  default = {}
}
