window.onload = () => getListOfFiles()

function sendRequest() {
    const xhr = new XMLHttpRequest()
    const url = document.getElementById("urlInput").value
    xhr.open('POST', 'api/File/download')
    xhr.setRequestHeader('Content-Type', 'application/json')
    xhr.onreadystatechange = function () {
        if (this.readyState === XMLHttpRequest.DONE && this.status === 200) {
            document.getElementById("responseText").innerHTML = `<a href="${this.responseText}" target="_blank">Direct Link</a>`
            getListOfFiles()
        }
    };
    xhr.send(`"${url}"`)
}

function getListOfFiles() {
    var filesList = document.getElementById("files-list")
    filesList.innerHTML = ""

    const xhr = new XMLHttpRequest()
    xhr.open('GET', 'api/File/list')
    xhr.setRequestHeader('Content-Type', 'application/json')
    xhr.onreadystatechange = function () {
        if (this.readyState === XMLHttpRequest.DONE && this.status === 200) {
            var fileData = JSON.parse(this.responseText)
            fileData.forEach(fd => {
                filesList.innerHTML += `<li>
              <a href="files/${fd.Name}" target="_blank">${fd.Name}</a>- 
              ${fd.FullName} - 
              ${fd.Type} - 
              ${fd.Length} - 
              ${fd.Extension}
              <button onclick="deleteSelectedFile('${fd.FullName}')">Delete</button>
              </li>`
            })
        }
    };
    xhr.send()
}

function deleteSelectedFile(fullPath) {

    const xhr = new XMLHttpRequest()
    xhr.open('POST', 'api/File/delete')
    xhr.setRequestHeader('Content-Type', 'application/json')
    xhr.onreadystatechange = function () {
        if (this.readyState === XMLHttpRequest.DONE && this.status === 200) {
            getListOfFiles()
        }
    };
    xhr.send(`"${fullPath}"`)
}

function getDownloadQueue() {
    var dql = document.getElementById("download-queue-list")
    dql.innerHTML = ""

    var xhr = new XMLHttpRequest()
    xhr.open("GET", "api/FileDownloadQueueItems")
    xhr.setRequestHeader("Content-Type", "application/json")
    xhr.onreadystatechange = () => {
        if (xhr.readyState === XMLHttpRequest.DONE && xhr.status === 200) {
            var data = JSON.parse(xhr.responseText)
            data.forEach(qi => {
                dql.innerHTML += `<li>
              ${qi.id} - 
              ${qi.inputUrl} - 
              ${qi.status} - 
              <button onclick="deleteSelectedFile('${qi.id}')">Delete</button>
              </li>`
            })
        }
    }
    xhr.send()
}