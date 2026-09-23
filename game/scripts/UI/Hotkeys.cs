using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
namespace RtsGame.UI;
public sealed class Hotkeys
{
    private readonly Key[] _slots=new Key[12];
    public Key this[int index]=>_slots[index];
    public Hotkeys()
    {
        Load("res://game/hotkeys.json");if(FileAccess.FileExists("user://hotkeys.json"))Load("user://hotkeys.json");
    }
    private void Load(string path)
    {
        try{var entries=JsonSerializer.Deserialize<Dictionary<string,string>>(FileAccess.GetFileAsString(path));if(entries==null)return;
            for(int i=0;i<12;i++)if(entries.TryGetValue($"slot{i}",out string? key)&&Enum.TryParse<Key>(key,true,out var code))_slots[i]=code;
        }catch(Exception e){GD.PushWarning("Hotkeys: "+e.Message);}
    }
    public bool Assign(int index,Key key)
    {
        if(key<Key.A || key>Key.Z || key is Key.W or Key.A or Key.S or Key.D)return false;
        for(int i=0;i<12;i++)if(i!=index && _slots[i]==key){_slots[i]=_slots[index];break;}
        _slots[index]=key;return true;
    }
    public void Save(){var entries=new Dictionary<string,string>();for(int i=0;i<12;i++)entries[$"slot{i}"]=_slots[i].ToString();using var file=FileAccess.Open("user://hotkeys.json",FileAccess.ModeFlags.Write);file.StoreString(JsonSerializer.Serialize(entries,new JsonSerializerOptions{WriteIndented=true}));}
}
