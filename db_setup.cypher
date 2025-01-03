// Create areas 
CREATE (warehouse:Area {type: "Warehouse", center_x: 0, center_z: 0, length: 50, width: 50})
CREATE (shipping:Area {type: "Shipping", center_x: -40, center_z: 0, length: 30, width: 50})
CREATE (delivery:Area {type: "Delivery", center_x: 40, center_z: 0, length: 30, width: 50})
CREATE (recharge:Area {type: "Recharge", center_x: 0, center_z: 30, length: 50, width: 10})

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
      CREATE (slot:Slot {x: slot_z})
      CREATE (layer)-[:HAS_SLOT]->(slot)
    )
  )
  CREATE (s)-[:LOCATED_IN]->(warehouse)
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