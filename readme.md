# ytPlayListLangStats

一個用來統計Youtube播放清單中由什麼語言組成的小工具

## feature
- 自動收集影片中聲軌的語言，抓的是`youtube data api v3`的影片`snippet`中的`defaultAudioLanguage`屬性
- 由於上述收集的資料有些影片沒有，如果該影片找不到此屬性，會先標註`ukunown`，需要在收集資料完畢後手動修正
## usage
- download: 初步下載統整播放清單中的語言,存入`viedos.json`
  - parameters:
    - --pl: youtube播放清單網址，預設值為[這組](https://www.youtube.com/playlist?list=PLdx_s59BrvfXJXyoU5BHpUkZGmZL0g3Ip)
    - --ytkey: youtube api key，預設從這個參數拿，拿不到會往 `appsettings.json` 找
  - Example
    - dotnet run download --pl youtubePlaylistURL
    - ytPlayListLangStats download --pl youtubePlaylistURL
- stat : 計算viedos.json中的語言種類與各有幾支影片，輸出到`result.json`
  - parameters:
    - --pl: youtube播放清單網址，預設值為[這組](https://www.youtube.com/playlist?list=PLdx_s59BrvfXJXyoU5BHpUkZGmZL0g3Ip)
    - --ytkey: youtube api key，預設從這個參數拿，拿不到會往 `appsettings.json` 找 
  - Example
    - dotnet run stat 
    - ytPlayListLangStats stat

**請將`appsettings_1.json`中的YoutubeAPIKey填上自己的Youtube Data Api Key，並將檔名改成`appsettings.json`**

**本專案有加 github actions 做自動收集資料，在動程式之前記得做 pull 之類的動作**