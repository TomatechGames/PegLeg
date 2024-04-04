extends Node

func _enter_tree() -> void:
	get_tree().node_added.connect(on_node_added)

func on_node_added(node: Node) -> void:
	var pp := node as PopupPanel
	if pp and pp.theme_type_variation == "TooltipPanel":
		pp.transparent_bg = true
