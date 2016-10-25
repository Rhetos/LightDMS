# LightDMS

LightDMS is a light document version system implementation (a plugin module) for [Rhetos development platform](https://github.com/Rhetos/Rhetos).
It automatically creates DocumentVersion and other entities for managing documents (and their version) in Rhetos based solutions.
Aside entities, versioning, it also exposes additional web interface for uploading/downloading files.

See [rhetos.org](http://www.rhetos.org/) for more information on Rhetos.

## Features

### Web service methods

**Upload:**

* Uploading a file with predefined document ID: `<RhetosSite>/LightDMS/Upload/{{ID}}`
    - Query parameters ID is required. ID is GUID formatted identificator of DocumentVersion.
    - Example format `http://localhost/Rhetos/LightDMS/Upload/8EF65043-2E2A-424D-B76F-4DAA5A48CB3D`

**Download:**

* Downloading a file with predefined document ID: `<RhetosSite>/LightDMS/Download/{{ID}}`
    - Query parameters ID is required. ID is GUID formatted identificator of DocumentVersion.
    - Example format `http://localhost/Rhetos/LightDMS/Download/8EF65043-2E2A-424D-B76F-4DAA5A48CB3D`
