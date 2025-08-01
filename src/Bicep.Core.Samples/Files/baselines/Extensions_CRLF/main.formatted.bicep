// BEGIN: Parameters

param strParam1 string

@secure()
param secureStrParam1 string

param boolParam1 bool

// END: Parameters

// BEGIN: Extension declarations

extension az
extension kubernetes  as k8s

//extension 'br:mcr.microsoft.com/bicep/extensions/microsoftgraph/v1:1.2.3' as graph

// END: Extension declarations

// BEGIN: Key vaults

resource kv1 'Microsoft.KeyVault/vaults@2019-09-01' existing = {
  name: 'kv1'
}

resource scopedKv1 'Microsoft.KeyVault/vaults@2019-09-01' existing = {
  name: 'scopedKv1'
  scope: az.resourceGroup('otherGroup')
}

// END: Key vaults

// BEGIN: Test resources

resource testResource1 'az:My.Rp/TestType@2020-01-01' = {
  name: 'testResource1'
  properties: {}
}

resource aks 'Microsoft.ContainerService/managedClusters@2024-02-01' = {
  name: 'aksCluster'
  location: az.resourceGroup().location
}

// END: Test resources

// BEGIN: Extension configs for modules

module moduleWithExtsWithAliases 'child/hasConfigurableExtensionsWithAlias.bicep' = {
  name: 'moduleWithExtsWithAliases'
  extensionConfigs: {
    k8s: {
      kubeConfig: 'kubeConfig2'
      namespace: 'ns2'
    }
  }
}

module moduleWithExtsWithoutAliases 'child/hasConfigurableExtensionsWithoutAlias.bicep' = {
  name: 'moduleWithExtsWithoutAliases'
  extensionConfigs: {
    kubernetes: {
      kubeConfig: 'kubeConfig2'
    }
  }
}

module moduleExtConfigsFromParams 'child/hasConfigurableExtensionsWithAlias.bicep' = {
  name: 'moduleExtConfigsFromParams'
  extensionConfigs: {
    k8s: {
      kubeConfig: boolParam1 ? secureStrParam1 : strParam1
      namespace: boolParam1 ? strParam1 : 'falseCond'
    }
  }
}

module moduleExtConfigFromKeyVaultReference 'child/hasConfigurableExtensionsWithAlias.bicep' = {
  name: 'moduleExtConfigKeyVaultReference'
  extensionConfigs: {
    k8s: {
      kubeConfig: kv1.getSecret('myKubeConfig')
      namespace: 'default'
    }
  }
}

module moduleExtConfigFromReferences 'child/hasConfigurableExtensionsWithAlias.bicep' = {
  name: 'moduleExtConfigFromReferences'
  extensionConfigs: {
    k8s: {
      kubeConfig: aks.listClusterAdminCredential().kubeconfigs[0].value
      namespace: testResource1.properties.namespace
    }
  }
}

module moduleWithExtsUsingFullInheritance 'child/hasConfigurableExtensionsWithAlias.bicep' = {
  name: 'moduleWithExtsFullInheritance'
  extensionConfigs: {
    k8s: k8s.config
  }
}

module moduleWithExtsUsingPiecemealInheritance 'child/hasConfigurableExtensionsWithAlias.bicep' = {
  name: 'moduleWithExtsPiecemealInheritance'
  extensionConfigs: {
    k8s: {
      kubeConfig: k8s.config.kubeConfig
      namespace: k8s.config.namespace
    }
  }
}

module moduleWithExtsUsingPiecemealInheritanceLooped 'child/hasConfigurableExtensionsWithAlias.bicep' = [
  for i in range(0, 4): {
    name: 'moduleWithExtsPiecemealInheritanceLooped${i}'
    extensionConfigs: {
      k8s: {
        kubeConfig: k8s.config.kubeConfig
        namespace: k8s.config.namespace
      }
    }
  }
]

module moduleExtConfigsConditionalMixed 'child/hasConfigurableExtensionsWithAlias.bicep' = {
  name: 'moduleExtConfigsConditionalMixedValueAndInheritance'
  extensionConfigs: {
    k8s: {
      kubeConfig: boolParam1 ? secureStrParam1 : k8s.config.kubeConfig
      namespace: boolParam1 ? az.resourceGroup().location : k8s.config.namespace
    }
  }
}

// END: Extension configs for modules
