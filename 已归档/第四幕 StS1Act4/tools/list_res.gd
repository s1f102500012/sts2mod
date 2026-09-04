extends SceneTree
func _init() -> void:
  var paths = [
    'res://scenes/backgrounds/overgrowth/layers',
    'res://scenes/backgrounds/hive/layers',
    'res://scenes/backgrounds/glory/layers'
  ]
  for p in paths:
    print('DIR ', p)
    var files = DirAccess.get_files_at(p)
    files.sort()
    for f in files:
      print('  ', f)
  quit()
