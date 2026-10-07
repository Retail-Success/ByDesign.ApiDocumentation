"""ByDesign API v2 - Python sample

Requires: Python 3.9+ and the requests package (pip install requests)
Set these environment variables first (values come from the ByDesign back office):
  BYDESIGN_API_URL        e.g. https://your-api-url (no trailing slash)
  BYDESIGN_CLIENT_ID
  BYDESIGN_CLIENT_SECRET
  BYDESIGN_API_KEY
"""
import os
import time

import requests


class ByDesignClient:
    """Minimal ByDesign API client that caches its access token and retries on 401 and 429."""

    def __init__(self, api_url, client_id, client_secret, api_key, scope="api/Read api/Write"):
        self.api_url = api_url.rstrip("/")
        self.client_id = client_id
        self.client_secret = client_secret
        self.scope = scope
        self.session = requests.Session()
        self.session.headers["x-api-key"] = api_key
        self._access_token = None
        self._expires_at = 0.0

    def _get_token(self):
        # Reuse the token until one minute before it expires
        if self._access_token and time.time() < self._expires_at - 60:
            return self._access_token

        response = self.session.post(
            f"{self.api_url}/oauth2/token",
            data={
                "grant_type": "client_credentials",
                "client_id": self.client_id,
                "client_secret": self.client_secret,
                "scope": self.scope,
            },
            timeout=30,
        )
        response.raise_for_status()
        token = response.json()
        self._access_token = token["access_token"]
        self._expires_at = time.time() + token["expires_in"]
        return self._access_token

    def request(self, method, path, **kwargs):
        for attempt in range(1, 5):
            headers = {"Authorization": f"Bearer {self._get_token()}"}
            response = self.session.request(method, f"{self.api_url}{path}", headers=headers, timeout=30, **kwargs)

            if response.status_code == 401 and attempt == 1:
                self._access_token = None  # Token expired or was revoked: fetch a new one and retry once
                continue
            if response.status_code == 429 and attempt <= 3:
                time.sleep(2 ** attempt)
                continue

            response.raise_for_status()
            return response.json() if response.content else None

    def get(self, path, **kwargs):
        return self.request("GET", path, **kwargs)

    def post(self, path, json=None, **kwargs):
        return self.request("POST", path, json=json, **kwargs)


if __name__ == "__main__":
    client = ByDesignClient(
        api_url=os.environ["BYDESIGN_API_URL"],
        client_id=os.environ["BYDESIGN_CLIENT_ID"],
        client_secret=os.environ["BYDESIGN_CLIENT_SECRET"],
        api_key=os.environ["BYDESIGN_API_KEY"],
    )

    # GET request: list inventory categories (requires api/Read)
    print(client.get("/api/v2/inventory/categorydetail"))

    # POST request: look up customers by email (requires api/Write)
    print(client.post("/api/v2/customer/lookup", json={"Email": "jane.doe@example.com"}))
