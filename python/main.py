from src.warehouse.warehouse_desk_app import WarehouseDeskApp

if __name__ == "__main__":
    app = WarehouseDeskApp()
    app.seed_data()
    app.run_demo_day()
