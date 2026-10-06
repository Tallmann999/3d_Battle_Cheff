"""Local MCP diagnostics; also usable before Codex reloads its MCP tool list."""
import asyncio
import json
import sys
from pathlib import Path
from fastmcp import Client


async def main():
    async with Client("http://127.0.0.1:8766/mcp", timeout=120) as client:
        action = sys.argv[1]
        instance_file = Path(".local-tools/unity-mcp/instance.txt")
        if instance_file.exists() and action in ("read", "call") and sys.argv[2] not in ("set_active_instance", "mcpforunity://instances"):
            await client.call_tool("set_active_instance", {"instance": instance_file.read_text().strip()})
        if action == "tools":
            selected = set(sys.argv[2:])
            for tool in await client.list_tools():
                if not selected or tool.name in selected:
                    print(json.dumps(tool.model_dump() if selected else {"name": tool.name}, ensure_ascii=False))
        elif action == "resources":
            for resource in await client.list_resources():
                print(json.dumps({"uri": str(resource.uri), "name": resource.name}, ensure_ascii=False))
        elif action == "read":
            for block in await client.read_resource(sys.argv[2]):
                print(getattr(block, "text", "[binary resource]"))
        elif action == "call":
            arguments = json.loads(Path(sys.argv[3]).read_text(encoding="utf-8-sig"))
            result = await client.call_tool(sys.argv[2], arguments)
            for block in result.content:
                if hasattr(block, "text"):
                    print(block.text)
                elif getattr(block, "type", "") == "image":
                    print("[image result]")
            if result.is_error:
                raise RuntimeError("MCP tool reported an error")
        else:
            raise ValueError("Use tools/resources/read/call")


if __name__ == "__main__":
    asyncio.run(main())
