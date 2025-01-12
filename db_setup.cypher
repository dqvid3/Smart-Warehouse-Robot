
// Create areas 
CREATE (warehouse:Area {type: "Warehouse", center_x: 0, center_z: 0, length: 50, width: 50})
CREATE (shipping:Area {type: "Shipping", center_x: -40, center_z: 0, length: 30, width: 50})
CREATE (delivery:Area {type: "Delivery", center_x: 40, center_z: 0, length: 30, width: 50})
CREATE (recharge:Area {type: "Recharge", center_x: 0, center_z: 30, length: 110, width: 10})
CREATE (backup:Area {type: "Backup", center_x: 0, center_z: -30, length: 110, width: 10})

// Create positions relative to the delivery area
FOREACH (pos_data IN [
  {z: 4},
  {z: 2},
  {z: 0},
  {z: -2},
  {z: -4}
] |
  CREATE (pos:Position {
    id: "delivery_" + toString(pos_data.z), 
    x: round(toFloat(delivery.center_x - 4.225 * delivery.length/10) * 1000.0) / 1000.0, 
    z: round(toFloat(delivery.center_z + pos_data.z * delivery.width/10) * 1000.0) / 1000.0,
    y: 0.995, 
    hasParcel: false
  })
  CREATE (delivery)-[:HAS_POSITION]->(pos)
)

// Create positions relative to the shipping area
FOREACH (pos_data IN [
  {z: 0},
  {z: 3},
  {z: -3}
] |
  CREATE (pos:Position {
    id: "shipping_" + toString(pos_data.z), 
    x: round(toFloat(shipping.center_x + 3.775 * shipping.length/10 + shipping.length/10 * -0.5) * 1000.0) / 1000.0, 
    z: round(toFloat(shipping.center_z + pos_data.z * shipping.width/10) * 1000.0) / 1000.0,
    y: 0.995,
    hasParcel: false
  })
  CREATE (shipping)-[:HAS_POSITION]->(pos)
)

// Create a position relative to the backup area
CREATE (backup_pos:Position {
  id: "backup_0", 
  x: 27.325, 
  z: -30,
  y: 0.995, 
  hasParcel: false
})
CREATE (backup)-[:HAS_POSITION]->(backup_pos)

// Create shelves with layers and slots
FOREACH (shelf IN [
  {x: 11, z: 20, category: "Books"},
  {x: -11, z: 20, category: "Toys"},
  {x: 11, z: 10, category: "Clothing"},
  {x: -11, z: 10, category: "Shoes"},
  {x: 11, z: 0, category: "Home Appliances"},
  {x: -11, z: 0, category: "Furniture"},
  {x: 11, z: -10, category: "Sports Equipment"},
  {x: -11, z: -10, category: "Beauty Products"},
  {x: 11, z: -20, category: "Electronics"},
  {x: -11, z: -20, category: "Groceries"}
] |
  // Create the shelf
  CREATE (s:Shelf {x: shelf.x, z: shelf.z, category: shelf.category})
  // Create layers for the shelf
  FOREACH (layer_id IN range(0, 3) |
    CREATE (layer:Layer {id: layer_id, y: 1.35 + layer_id * 1.48})
    CREATE (s)-[:HAS_LAYER]->(layer)
    // Create slots for the layer (with relative z positions)
    FOREACH (slot_z IN [6.7, 4.7, 2.9, 0.9, -0.9, -2.9, -4.7, -6.7] |
      CREATE (slot:Slot {x: slot_z, occupied: false})
      CREATE (layer)-[:HAS_SLOT]->(slot)
    )
  )
  CREATE (s)-[:LOCATED_IN]->(warehouse)
)

// Create a shelf in the backup area with category "Backup"
CREATE (backup_shelf:Shelf {x: 0, z: -30, category: "Backup"})
// Create layers for the backup shelf
FOREACH (layer_id IN range(0, 3) |
  CREATE (layer:Layer {id: layer_id, y: 1.35 + layer_id * 1.48})
  CREATE (backup_shelf)-[:HAS_LAYER]->(layer)
  // Create slots for the layer (with relative z positions)
  FOREACH (slot_z IN [6.7, 4.7, 2.9, 0.9, -0.9, -2.9, -4.7, -6.7] |
    CREATE (slot:Slot {x: slot_z, occupied: false})
    CREATE (layer)-[:HAS_SLOT]->(slot)
  )
)
CREATE (backup_shelf)-[:LOCATED_IN]->(backup)

// Create robots
FOREACH (robot_data IN [
  {id: 0, x: -15, z: 30, active: true, task: "None", state: "Idle", battery: 100},
  {id: 1, x: 0, z: 30, active: true, task: "None", state: "Idle", battery: 100},
  {id: 2, x: 15, z: 0, active: true, task: "None", state: "Idle", battery: 100}
] |
  CREATE (:Robot {
    id: robot_data.id, 
    x: robot_data.x, 
    z: robot_data.z, 
    active: robot_data.active, 
    task: robot_data.task, 
    state: robot_data.state,
    battery: robot_data.battery
  })
);

// Create products
FOREACH (product_data IN [
  {category: "Books", product_name: "The Great Gatsby"},
  {category: "Toys", product_name: "Lego Set"},
  {category: "Clothing", product_name: "T-Shirt"},
  {category: "Shoes", product_name: "Running Shoes"},
  {category: "Home Appliances", product_name: "Microwave"},
  {category: "Furniture", product_name: "Office Chair"},
  {category: "Sports Equipment", product_name: "Basketball"},
  {category: "Beauty Products", product_name: "Lipstick"},
  {category: "Electronics", product_name: "Smartphone"},
  {category: "Groceries", product_name: "Cereal"}
] |
  CREATE (p:Product {product_name: product_data.product_name, category: product_data.category})
);

// Link each robot to every area 
MATCH (r:Robot), (a:Area)
MERGE (r)-[:OPERATES_IN]->(a);

FOREACH (landmark IN [
  {id: 1, x: -10.97, z: -0.80},
  {id: 2, x: -18.85, z: 0.00},
  {id: 3, x: -3.15, z: 0.00},
  {id: 4, x: -29.50, z: 21.00},
  {id: 5, x: 28.00, z: -5.00},
  {id: 6, x: -12.25, z: 34.50},
  {id: 7, x: 11.03, z: 19.20},
  {id: 8, x: 3.15, z: 20.00},
  {id: 9, x: 18.85, z: 20.00},
  {id: 10, x: 0.00, z: 34.50},
  {id: 11, x: 24.50, z: 34.50},
  {id: 12, x: -10.97, z: -10.80},
  {id: 13, x: -18.85, z: -10.00},
  {id: 14, x: -3.15, z: -10.00},
  {id: 15, x: -10.97, z: 9.20},
  {id: 16, x: -18.85, z: 10.00},
  {id: 17, x: -3.15, z: 10.00},
  {id: 18, x: 28.00, z: 5.00},
  {id: 19, x: -25.00, z: -24.50},
  {id: 20, x: 11.03, z: -20.92},
  {id: 21, x: 3.15, z: -20.00},
  {id: 22, x: 18.85, z: -20.00},
  {id: 23, x: 28.00, z: -15.00},
  {id: 24, x: -24.50, z: 34.50},
  {id: 25, x: 28.00, z: 15.00},
  {id: 26, x: -29.50, z: 7.50},
  {id: 27, x: 25.00, z: -24.50},
  {id: 28, x: -29.50, z: -21.00},
  {id: 29, x: -24.50, z: 25.50},
  {id: 30, x: 11.03, z: 9.20},
  {id: 31, x: 3.15, z: 10.00},
  {id: 32, x: 18.85, z: 10.00},
  {id: 33, x: -10.97, z: 19.20},
  {id: 34, x: -18.85, z: 20.00},
  {id: 35, x: -3.15, z: 20.00},
  {id: 36, x: 11.03, z: -10.80},
  {id: 37, x: 3.15, z: -10.00},
  {id: 38, x: 18.85, z: -10.00},
  {id: 39, x: -10.97, z: -20.80},
  {id: 40, x: -18.85, z: -20.00},
  {id: 41, x: -3.15, z: -20.00},
  {id: 42, x: 11.03, z: -0.80},
  {id: 43, x: 3.15, z: 0.00},
  {id: 44, x: 18.85, z: 0.00},
  {id: 45, x: -29.50, z: -7.50}
] |
  CREATE (:Landmark {id: landmark.id, x: landmark.x, z: landmark.z})
);

