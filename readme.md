# ytPlayListLangStats

一個用來統計Youtube播放清單中由什麼語言組成的小工具

## feature
- 自動收集影片中聲軌的語言，抓的是`youtube data api v3`的影片`snippet`中的`defaultAudioLanguage`屬性
- 由於上述收集的資料有些影片沒有，如果該影片找不到此屬性，會先標註`ukunown`，需要在收集資料完畢後手動修正
## usage
Commands:
  - download: 初步下載統整播放清單中的語言,存入`viedos.json`
      - Example
          - dotnet run download 
          - ytPlayListLangStats download
  - stat : 計算viedos.json中的語言種類與各有幾支影片，輸出到`result.json`
      - Example
          - dotnet run stat 
          - ytPlayListLangStats stat

**請將`appsettings_1.json`中的YoutubeAPIKey填上自己的Youtube Data Api Key，並將檔名改成`appsettings.json`**