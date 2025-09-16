docker build -t localhost:5000/systems-manager:latest .
docker push localhost:5000/systems-manager:latest
kubectl rollout restart deployment systems-manager-deployment -n sdmd