"""GET /me – returns the current user's identity from the JWT token."""
from fastapi import APIRouter, Depends
from pydantic import BaseModel

from ..security_jwt import require_home_user

router = APIRouter(tags=["me"])


class MeResponse(BaseModel):
    sub: str
    email: str | None
    name: str | None
    roles: list[str]


@router.get("/me", response_model=MeResponse)
def get_me(user: dict = Depends(require_home_user)) -> MeResponse:
    """Return identity claims from the current bearer token."""
    roles: list[str] = (user.get("realm_access") or {}).get("roles", [])
    return MeResponse(
        sub=user.get("sub", ""),
        email=user.get("email"),
        name=user.get("name"),
        roles=roles,
    )
