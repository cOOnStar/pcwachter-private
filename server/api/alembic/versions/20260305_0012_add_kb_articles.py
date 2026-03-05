"""add kb_articles table

Revision ID: 20260305_0012
Revises: 20260305_0011
Create Date: 2026-03-05
"""

from alembic import op
import sqlalchemy as sa
from sqlalchemy.dialects import postgresql


# revision identifiers, used by Alembic.
revision = "20260305_0012"
down_revision = "20260305_0011"
branch_labels = None
depends_on = None


def upgrade() -> None:
    if not _table_exists("kb_articles"):
        op.create_table(
            "kb_articles",
            sa.Column(
                "id",
                postgresql.UUID(as_uuid=True),
                nullable=False,
                server_default=sa.text("gen_random_uuid()"),
            ),
            sa.Column("title", sa.String(length=200), nullable=False),
            sa.Column("category", sa.String(length=64), nullable=False, server_default="general"),
            sa.Column(
                "tags",
                postgresql.JSONB(astext_type=sa.Text()),
                nullable=False,
                server_default="[]",
            ),
            sa.Column("body_md", sa.Text(), nullable=False, server_default=""),
            sa.Column("published", sa.Boolean(), nullable=False, server_default="true"),
            sa.Column(
                "created_at",
                sa.DateTime(timezone=True),
                nullable=False,
                server_default=sa.text("now()"),
            ),
            sa.Column(
                "updated_at",
                sa.DateTime(timezone=True),
                nullable=False,
                server_default=sa.text("now()"),
            ),
            sa.PrimaryKeyConstraint("id"),
        )

    if not _index_exists("ix_kb_articles_updated_at"):
        op.create_index("ix_kb_articles_updated_at", "kb_articles", ["updated_at"], unique=False)
    if not _index_exists("ix_kb_articles_title"):
        op.create_index("ix_kb_articles_title", "kb_articles", ["title"], unique=False)
    if not _index_exists("ix_kb_articles_published"):
        op.create_index("ix_kb_articles_published", "kb_articles", ["published"], unique=False)


def downgrade() -> None:
    if not _table_exists("kb_articles"):
        return

    for idx in ("ix_kb_articles_published", "ix_kb_articles_title", "ix_kb_articles_updated_at"):
        if _index_exists(idx):
            op.drop_index(idx, table_name="kb_articles")
    op.drop_table("kb_articles")


def _table_exists(table_name: str) -> bool:
    bind = op.get_bind()
    result = bind.execute(
        sa.text("SELECT to_regclass(:name)"),
        {"name": f"public.{table_name}"},
    ).scalar()
    return result is not None


def _index_exists(index_name: str) -> bool:
    bind = op.get_bind()
    result = bind.execute(
        sa.text("SELECT to_regclass(:name)"),
        {"name": f"public.{index_name}"},
    ).scalar()
    return result is not None
