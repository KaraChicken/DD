#!/usr/bin/env python3
"""Restore SQL Server backups in an ephemeral CI container and export metadata only.

No table rows, credentials, backup files, or database files are uploaded as artifacts.
"""
from __future__ import annotations

import json
import os
import pathlib
import subprocess
import sys
import time

ROOT = pathlib.Path(__file__).resolve().parents[2]
BACKUPS = [
    ROOT / "Db" / "Db_Membership_20200702.BAK",
    ROOT / "Db" / "Db_Tank41.BAK",
    ROOT / "Db" / "Db_Tank_20200702.BAK",
]
CONTAINER = "ddtank-sqlserver-inspect"
PASSWORD = os.environ["MSSQL_SA_PASSWORD"]
SQLCMD = "/opt/mssql-tools18/bin/sqlcmd"
REPORT = ROOT / "artifacts" / "database-inspection"
REPORT.mkdir(parents=True, exist_ok=True)


def docker(*args: str, input_text: str | None = None) -> str:
    result = subprocess.run(
        ["docker", *args], input=input_text, text=True,
        stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
    )
    if result.returncode != 0:
        raise RuntimeError(
            f"Command failed (exit {result.returncode}): docker {' '.join(args)}\\n{result.stdout}"
        )
    return result.stdout


def sql(query: str) -> str:
    return docker(
        "exec", "-i", CONTAINER, SQLCMD,
        "-S", "localhost", "-U", "sa", "-P", PASSWORD, "-C",
        "-b", "-h", "-1", "-s", "|", "-y", "0", "-w", "65535", "-Q", query,
    )


def quote_sql(value: str) -> str:
    return "N'" + value.replace("'", "''") + "'"


def rows(output: str) -> list[list[str]]:
    parsed = []
    for line in output.splitlines():
        parts = [part.strip() for part in line.split("|")]
        if len(parts) >= 3 and parts[2] in {"D", "L"} and parts[0]:
            parsed.append(parts)
    return parsed


def selected_procedure_definitions(db_name: str, names: list[str]) -> list[dict]:
    quoted_names = ", ".join(quote_sql(name) for name in names)
    query = f"""
    SET NOCOUNT ON;
    SELECT p.name, m.definition
    FROM [{db_name}].sys.procedures AS p
    LEFT JOIN [{db_name}].sys.sql_modules AS m ON m.object_id = p.object_id
    WHERE p.name IN ({quoted_names})
    ORDER BY p.name
    FOR JSON PATH;
    """
    output = sql(query)
    payload = "".join(line.strip() for line in output.splitlines() if line.strip())
    if not payload:
        return []
    return json.loads(payload)


def inspect_backup(backup: pathlib.Path, index: int) -> dict:
    if not backup.is_file():
        raise FileNotFoundError(backup)
    remote = f"/var/opt/mssql/backup/{backup.name}"
    docker("cp", str(backup), f"{CONTAINER}:{remote}")
    logical_files = rows(sql(f"RESTORE FILELISTONLY FROM DISK = {quote_sql(remote)}"))
    if not logical_files:
        raise RuntimeError(f"Could not parse RESTORE FILELISTONLY for {backup.name}")

    db_name = f"DDTankBackupInspect{index}"
    moves = []
    for file_index, item in enumerate(logical_files, start=1):
        logical_name, file_type = item[0], item[2]
        extension = "mdf" if file_type == "D" and file_index == 1 else (
            "ndf" if file_type == "D" else "ldf"
        )
        target = f"/var/opt/mssql/data/{db_name}_{file_index}.{extension}"
        moves.append(f"MOVE {quote_sql(logical_name)} TO {quote_sql(target)}")

    restore = (
        f"RESTORE DATABASE [{db_name}] FROM DISK = {quote_sql(remote)} "
        f"WITH {', '.join(moves)}, REPLACE, RECOVERY"
    )
    sql(restore)

    objects_query = f"""
    SET NOCOUNT ON;
    SELECT o.type_desc, s.name, o.name
    FROM [{db_name}].sys.objects AS o
    JOIN [{db_name}].sys.schemas AS s ON s.schema_id = o.schema_id
    WHERE o.is_ms_shipped = 0
    ORDER BY o.type_desc, s.name, o.name;
    """
    object_lines = [
        line.strip() for line in sql(objects_query).splitlines()
        if line.strip() and "|" in line and not line.lstrip().startswith("(")
    ]

    columns_query = f"""
    SET NOCOUNT ON;
    SELECT s.name, t.name, c.name, ty.name,
           CASE WHEN ty.name IN ('nvarchar','nchar','varchar','char','varbinary','binary')
                THEN CONVERT(varchar(12), c.max_length) ELSE '' END,
           c.is_nullable, c.is_identity
    FROM [{db_name}].sys.tables AS t
    JOIN [{db_name}].sys.schemas AS s ON s.schema_id = t.schema_id
    JOIN [{db_name}].sys.columns AS c ON c.object_id = t.object_id
    JOIN [{db_name}].sys.types AS ty ON ty.user_type_id = c.user_type_id
    ORDER BY s.name, t.name, c.column_id;
    """
    column_lines = [
        line.strip() for line in sql(columns_query).splitlines()
        if line.strip() and "|" in line and not line.lstrip().startswith("(")
    ]

    counts_query = f"""
    SET NOCOUNT ON;
    SELECT 'tables', COUNT(*) FROM [{db_name}].sys.tables WHERE is_ms_shipped = 0
    UNION ALL SELECT 'views', COUNT(*) FROM [{db_name}].sys.views WHERE is_ms_shipped = 0
    UNION ALL SELECT 'procedures', COUNT(*) FROM [{db_name}].sys.procedures WHERE is_ms_shipped = 0
    UNION ALL SELECT 'triggers', COUNT(*) FROM [{db_name}].sys.triggers WHERE is_ms_shipped = 0;
    """

    row_counts_query = f"""
    SET NOCOUNT ON;
    SELECT s.name, t.name, SUM(p.rows)
    FROM [{db_name}].sys.tables AS t
    JOIN [{db_name}].sys.schemas AS s ON s.schema_id = t.schema_id
    JOIN [{db_name}].sys.partitions AS p ON p.object_id = t.object_id
    WHERE p.index_id IN (0, 1)
    GROUP BY s.name, t.name
    ORDER BY s.name, t.name;
    """
    row_count_lines = [
        line.strip() for line in sql(row_counts_query).splitlines()
        if line.strip() and "|" in line and not line.lstrip().startswith("(")
    ]
    database_query = f"""
    SET NOCOUNT ON;
    SELECT d.name, d.compatibility_level, d.collation_name, d.recovery_model_desc
    FROM sys.databases AS d WHERE d.name = {quote_sql(db_name)};
    """
    database_lines = [
        line.strip() for line in sql(database_query).splitlines()
        if line.strip() and "|" in line and not line.lstrip().startswith("(")
    ]
    count_lines = [
        line.strip() for line in sql(counts_query).splitlines()
        if line.strip() and "|" in line and not line.lstrip().startswith("(")
    ]
    procedure_names = []
    if "Membership" in backup.name:
        procedure_names = [
            "Mem_Users_CreateUser", "Mem_UserInfo_Addmin", "Mem_UserInfo_SearchName",
            "st_Login", "st_GetUserInfo", "Webshop_Register"
        ]
    elif "Tank41" in backup.name:
        procedure_names = [
            "SP_Users_Active", "SP_CheckAccount", "SP_Account_Register",
            "SP_Users_RegisterNotValidate", "SP_Users_RegisterNotValidate2",
            "SP_Users_Login", "SP_Users_LoginWeb", "SP_GetPlayerByUserName",
            "SP_Users_SingleByUserName", "SP_Users_SingleByUserID",
            "SP_Users_CheckByNickName", "SP_Users_Update", "SP_User_Info_Add"
        ]
    procedure_definitions = selected_procedure_definitions(db_name, procedure_names)

    return {
        "backup_file": backup.name,
        "backup_size_bytes": backup.stat().st_size,
        "restored_database_name": db_name,
        "logical_files": [
            {"logical_name": item[0], "type": item[2]} for item in logical_files
        ],
        "database_metadata": database_lines,
        "object_counts": count_lines,
        "table_row_counts": row_count_lines,
        "objects": object_lines,
        "columns": column_lines,
        "selected_procedure_definitions": procedure_definitions,
    }


def static_provider_audit() -> dict:
    source = ROOT / "Source Server"
    procedure_calls: set[str] = set()
    sql_server_refs: set[str] = set()
    cs_files = list(source.rglob("*.cs"))
    for path in cs_files:
        try:
            content = path.read_text(encoding="utf-8-sig", errors="ignore")
        except OSError:
            continue
        import re
        procedure_calls.update(re.findall(
            r'(?:RunProcedure|ExecuteNonQuery|ExecuteScalar|GetDataTable)\s*\([^;]{0,500}?"(SP_[A-Za-z0-9_]+)"',
            content, flags=re.S,
        ))
        if "System.Data.SqlClient" in content or "SqlConnection" in content or "SqlCommand" in content:
            sql_server_refs.add(path.relative_to(ROOT).as_posix())
    return {
        "csharp_files_scanned": len(cs_files),
        "files_using_sqlclient": sorted(sql_server_refs),
        "stored_procedure_names_found_by_static_scan": sorted(procedure_calls),
    }


def main() -> None:
    # SQL Server is ephemeral; only metadata is retained.
    docker("exec", "-u", "0", CONTAINER, "mkdir", "-p", "/var/opt/mssql/backup")
    docker("exec", "-u", "0", CONTAINER, "chmod", "777", "/var/opt/mssql/backup")
    results = []
    for index, backup in enumerate(BACKUPS, start=1):
        print(f"Inspecting {backup.name} ({backup.stat().st_size} bytes)", flush=True)
        results.append(inspect_backup(backup, index))
    report = {
        "disclaimer": "Metadata only. No table data was exported.",
        "backups": results,
        "static_provider_audit": static_provider_audit(),
    }
    (REPORT / "database-inventory.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print(f"Wrote metadata report: {REPORT / 'database-inventory.json'}")


if __name__ == "__main__":
    main()
