using System;
using System.Buffers.Binary;
using System.IO;

namespace RtsGame.Sim.World;

public static class MapLoader
{
    public const int ByteLength = 12 + Grid.Size * Grid.Size * 6;
    private const uint Magic = 0x504D4741; // AGMP
    public static byte[] Save(MapData map)
    {
        var bytes = new byte[ByteLength];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, Magic);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), Grid.Size);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), Grid.Size);
        int offset = 12;
        for (int y = 0; y < Grid.Size; y++)
        for (int x = 0; x < Grid.Size; x++)
        {
            Tile tile = map.Grid[x,y];
            bytes[offset++] = (byte)((tile.Walkable ? 1 : 0) | (tile.Buildable ? 2 : 0));
            bytes[offset++] = tile.Height;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), tile.ResourceNodeId);
            offset += 4;
        }
        return bytes;
    }
    public static MapData Load(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != ByteLength || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != Magic ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(4)) != 1 ||
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(8)) != Grid.Size ||
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(10)) != Grid.Size)
            throw new InvalidDataException("Unsupported map header or length.");
        var tiles = new Tile[Grid.Size * Grid.Size];
        int offset = 12;
        for (int i = 0; i < tiles.Length; i++)
        {
            byte flags = bytes[offset++];
            byte height = bytes[offset++];
            int resource = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset));
            offset += 4;
            if (flags > 3 || flags == 2 || height > 2 || resource < -1)
                throw new InvalidDataException("Invalid tile data.");
            tiles[i] = new Tile((flags & 1) != 0, (flags & 2) != 0, height, resource);
        }
        return new MapData(new Grid(tiles));
    }
}
