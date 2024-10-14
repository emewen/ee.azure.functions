#vars

variable "locationName" {
  type = string
  description = "Deployment location friendly name"
  default = "Australia East"
}

variable "rgName" {
  type = string
  description = "Resource Group Name"
  default = "ericewentenant-stock-price-RG"
}

variable "cosmosName" {
  type = string
  description = "Cosmos Account Name"
  default = "cosmos-stock-price"
}
