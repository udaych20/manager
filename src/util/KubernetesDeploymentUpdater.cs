using k8s;
using k8s.Models;

public class KubernetesDeploymentUpdater
{
    private readonly Kubernetes kubernetesClient;
    private readonly ILogger<KubernetesDeploymentUpdater> _logger;

    public KubernetesDeploymentUpdater(Kubernetes client, ILoggerFactory _loggerFactory)
    {
        kubernetesClient = client;
        _logger = _loggerFactory.CreateLogger<KubernetesDeploymentUpdater>();
    }

    public void UpdateDeploymentLabel(string deploymentName, string namespaceName, string labelKey, string labelValue)
    {
        try
        {
            // Read the existing deployment
            var deployment = kubernetesClient.ReadNamespacedDeployment(deploymentName, namespaceName);

            // Ensure the labels dictionary in the template metadata is initialized
            if (deployment.Spec.Template.Metadata.Labels == null)
                deployment.Spec.Template.Metadata.Labels = new Dictionary<string, string>();

            // Update the 'workloadType' label in the template metadata
            deployment.Spec.Template.Metadata.Labels[labelKey] = labelValue;

            // Also update the selector if it's a new label
            if (!deployment.Spec.Selector.MatchLabels.ContainsKey(labelKey))
            {
                deployment.Spec.Selector.MatchLabels.Add(labelKey, labelValue);
            }
            else
            {
                deployment.Spec.Selector.MatchLabels[labelKey] = labelValue;
            }


            // Replace the deployment with the updated template
            var updatedDeployment = kubernetesClient.ReplaceNamespacedDeployment(deployment, deploymentName, namespaceName);
            _logger.LogInformation("{labelKey} label updated to {labelValue} for deployment '{DeploymentName}'", labelKey, labelValue, deploymentName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update deployment '{DeploymentName}'", deploymentName);
            throw; // Consider how you want to handle exceptions based on your application's needs
        }

    }
}
