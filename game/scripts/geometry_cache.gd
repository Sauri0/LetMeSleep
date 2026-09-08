extends RefCounted
## Eviction removes cache ownership only. In-use immutable maps remain valid.
static func touch(order: Array[String], key: String, limit: int, caches: Array) -> void:
	if not order.is_empty() and order.back()==key: return
	order.erase(key)
	order.append(key)
	while order.size()>limit:
		var expired: String=order.pop_front()
		for cache: Dictionary in caches: cache.erase(expired)
