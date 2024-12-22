// Create areas
CREATE (warehouse:Area {type: "Warehouse", center_x: 0, center_z: 0, length: 50, width: 50})
CREATE (shipping:Area {type: "Shipping", center_x: -35, center_z: 0, length: 20, width: 50})
CREATE (delivery:Area {type: "Delivery", center_x: 35, center_z: 0, length: 20, width: 50})
CREATE (recharge:Area {type: "Recharge", center_x: 0, center_z: 30, length: 50, width: 10})

// Create robots
CREATE (:Robot {x: 0, z: 0, state: "inactive", battery: 10})
CREATE (:Robot {x: 0, z: 0, state: "inactive", battery: 100})

// Create shelves with layers and slots
FOREACH (shelf IN [
  {x: -5, z: -5, length: 4, width: 2, category: "Condoms"},
  {x: 5, z: -5, length: 4, width: 2, category: "Mechanics"},
  {x: -5, z: 0, length: 4, width: 2, category: "Food"},
  {x: 5, z: 0, length: 4, width: 2, category: "Electronics"}
] |
  // Create the shelf
  CREATE (s:Shelf {length: shelf.length, width: shelf.width, x: shelf.x, z: shelf.z, category: shelf.category})

  // Create layers for the shelf
  FOREACH (layer_id IN range(0, 1) |
    CREATE (layer:Layer {id: layer_id, y: 1 + layer_id * 1.5})
    CREATE (s)-[:HAS_LAYER]->(layer)

    // Create slots for the layer (partially filled)
    FOREACH (slot_id IN range(0, 3) |
      CREATE (slot:Slot {x: (slot_id - 1.5)})
      CREATE (layer)-[:HAS_SLOT]->(slot)
    )
  )
  CREATE (s)-[:LOCATED_IN]->(warehouse)
);

// Link each robot to every area
MATCH (r:Robot), (a:Area)
MERGE (r)-[:OPERATES_IN]->(a)