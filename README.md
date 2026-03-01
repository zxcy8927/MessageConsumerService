#MessageConsumerService

這是一個使用 .NET 8 開發的 Worker Service，主要功能是從資料庫佇列中持續讀取並處理訊息，負責監聽 Message Queue， 接收訊息後進行解析、驗證與業務處理，並支援錯誤重試與例外紀錄。

🔧 技術棧

.NET 8

Worker Service

Dapper

System.Data.SqlClient


📁 專案結構說明
/
├── Domain/
│   └── MessageEntity.cs        
│       # 定義訊息資料模型

├── Repository/
│   └── MessageQueueRepository.cs 
│       # 負責資料庫操作（撈資料、認領訊息、更新狀態）

├── Services/
│   └── MessageHandler.cs       
│       # 真正處理訊息內容的商業邏輯

├── MessageConsumerService.Tests/
│       # 單元測試專案

├── Worker.cs                   
│       # 背景服務主流程（輪詢 + 呼叫處理邏輯）

├── Program.cs                  
│       # DI 註冊與服務啟動設定

└── appsettings.json            
        # 設定檔（資料庫連線字串等）

專案分層：

Repository 專門處理資料庫

Service 專門處理邏輯

Worker 只負責流程控制

這樣後續要擴充或改成別的佇列來源（例如 MQ）會比較好調整。

⚙️ 安裝與設定
1️⃣ 安裝 .NET 8 SDK

請先安裝 .NET 8 SDK
👉 https://dotnet.microsoft.com/download/dotnet/8.0

2️⃣ 下載專案
git clone <your-repository-url>
cd MessageConsumerService
3️⃣ 設定資料庫連線字串

在 appsettings.json 中設定：

{
  "ConnectionStrings": {
    "DefaultConnection": "Server=your_server;Database=your_database;User Id=your_user;Password=your_password;"
  }
}

啟動後服務會：

從資料庫撈取尚未處理的訊息

嘗試 Claim 該筆資料

執行處理邏輯

更新處理結果狀態

會持續循環直到服務關閉。
