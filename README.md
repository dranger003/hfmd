Simple Hugging Face model/dataset downloader, with custom selections and supports simultaneous downloads.  
To download a model:
```
hfmd <model-id> <dest-path>
````
Example:
```
hfmd LargeWorldModel/LWM-Text-Chat-512K D:\LLM_MODELS\
```
For downloading a dataset:
```
hfmd dataset:<dataset-id> <dest-path>
````
Example:
```
hfmd dataset:ikawrakow/validation-datasets-for-llama.cpp C:\LLM_MODELS\
```
Screenshot:

![Screenshot 2024-02-17 210430](https://github.com/dranger003/hfmd/assets/1760549/51333f5f-f477-4ef6-ad0d-b323104e3dbd)
