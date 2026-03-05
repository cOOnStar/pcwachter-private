import requests

class ZammadClient:
    def __init__(self, base_url: str, token: str):
        self.base = base_url.rstrip("/")
        self.s = requests.Session()
        self.s.headers.update({"Authorization": f"Token token={token}", "Content-Type":"application/json"})

    def find_user_by_email(self, email: str):
        # Zammad search API varies; adjust endpoint to your deployment.
        r = self.s.get(f"{self.base}/api/v1/users/search", params={"query": email})
        r.raise_for_status()
        data = r.json()
        return data[0] if data else None

    def create_user(self, email: str, firstname="PCW", lastname="User"):
        payload = {"email": email, "firstname": firstname, "lastname": lastname, "role_ids":[3]} # example role id
        r = self.s.post(f"{self.base}/api/v1/users", json=payload)
        r.raise_for_status()
        return r.json()
