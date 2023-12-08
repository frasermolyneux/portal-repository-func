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

variable "api_management_subscription_id" {}
variable "api_management_resource_group_name" {}
variable "api_management_name" {}

variable "web_apps_subscription_id" {}
variable "web_apps_resource_group_name" {}
variable "web_apps_app_service_plan_name" {}

variable "geo_location_api" {
  type = object({
    application_name     = string
    application_audience = string
    apim_api_name        = string
    apim_api_revision    = string
    apim_path_prefix     = string
  })
  default = {
    application_name     = "geolocation-api-dev-01"
    application_audience = "api://geolocation-api-dev-01"
    apim_api_name        = "geolocation-api"
    apim_api_revision    = "1"
    apim_path_prefix     = "geolocation"
  }
}

variable "repository_api" {
  type = object({
    application_name     = string
    application_audience = string
    apim_api_name        = string
    apim_api_revision    = string
    apim_path_prefix     = string
  })
  default = {
    application_name     = "portal-repository-dev-01"
    application_audience = "api://portal-repository-dev-01"
    apim_api_name        = "repository-api"
    apim_api_revision    = "1"
    apim_path_prefix     = "repository"
  }
}

variable "servers_integration_api" {
  type = object({
    application_name     = string
    application_audience = string
    apim_api_name        = string
    apim_api_revision    = string
    apim_path_prefix     = string
  })
  default = {
    application_name     = "portal-servers-integration-dev-01"
    application_audience = "api://portal-servers-integration-dev-01"
    apim_api_name        = "servers-integration-api"
    apim_api_revision    = "1"
    apim_path_prefix     = "servers-integration"
  }
}

variable "tags" {
  default = {}
}
