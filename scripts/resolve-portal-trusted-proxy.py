"""Resolve the exact host proxy address from Docker network metadata only."""
import ipaddress
import json
import sys


def resolve_gateway(network, project):
    if (
        network.get("Name") != f"{project}_default"
        or network.get("Driver") != "bridge"
        or network.get("Labels", {}).get("com.docker.compose.project") != project
        or network.get("Labels", {}).get("com.docker.compose.network") != "default"
    ):
        raise ValueError("Portal requires the project's default Compose bridge network")
    gateways = []
    for config in network.get("IPAM", {}).get("Config", []):
        gateway = ipaddress.ip_address(config["Gateway"])
        subnet = ipaddress.ip_network(config["Subnet"])
        if gateway.version != 4:
            continue
        if gateway not in subnet or gateway.is_unspecified or gateway.is_loopback or gateway.is_multicast:
            raise ValueError("Portal bridge gateway is not a specific IPv4 proxy address")
        gateways.append(str(gateway))
    if len(gateways) != 1:
        raise ValueError("Portal bridge must have exactly one IPv4 gateway")
    return gateways[0]


if __name__ == "__main__":
    try:
        print(resolve_gateway(json.load(sys.stdin), sys.argv[1]))
    except (ValueError, KeyError, TypeError, IndexError) as error:
        raise SystemExit(f"Cannot derive PORTAL_TRUSTED_PROXY_IP: {error}") from error
