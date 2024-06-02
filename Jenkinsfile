pipeline {
    
    agent none

    stages {
      
        stage('Build') {

            agent {
                dockerfile {                   
                    args '-u root:root'
                }
            }

            steps {
                
                echo sh(script: 'env|sort', returnStdout: true)

                sh 'dotnet build ./Oragon.RabbitMQ.sln'

            }

        }
     

        stage('Test') {

            agent {
                dockerfile {
                    // alwaysPull false
                    // image 'microsoft/dotnet:2.1-sdk'
                    // reuseNode false
                    args '-u root:root -v /var/run/docker.sock:/var/run/docker.sock'
                }
            }

            steps {

                withCredentials([usernamePassword(credentialsId: 'SonarQube', passwordVariable: 'SONARQUBE_KEY', usernameVariable: 'DUMMY' )]) 
                {

                    script 
                    {

                        // sonarcloud issue | https://community.sonarsource.com/t/could-not-find-ref-refs-heads-master-in-refs-heads-refs-remotes-upstream-or-refs-remotes-origin/37016/5
                        sh ''' git fetch origin master:master  '''
                        sh ''' git fetch origin develop:develop  '''

                        def sonarParams = [
                            '/k:"Oragon.RabbitMQ"',
                            '/o:luizcarlosfaria',
                            '/d:sonar.token="$SONARQUBE_KEY"',
                            '/d:sonar.host.url="https://sonarcloud.io"',
                            '/d:sonar.cs.vscoveragexml.reportsPaths=/output-coverage/coverage.xml'                            
                        ]

                        if ((env.BRANCH_NAME == "develop") || (env.BRANCH_NAME == "master")) {
                    
                            // reservado nesses nesses casos não é necessário

                        } else if (env.BRANCH_NAME.startsWith('feature/')) {
                            
                            sonarParams << '/d:sonar.branch.name="$BRANCH_NAME"'
                            sonarParams << '/d:sonar.branch.target=develop'

                        } else if (env.BRANCH_NAME.startsWith('hotfix/')) {

                            sonarParams << '/d:sonar.branch.name="$BRANCH_NAME"'
                            sonarParams << '/d:sonar.branch.target=master'

                        } else if (env.BRANCH_NAME.startsWith('release/')) {

                            sonarParams << '/d:sonar.branch.name="$BRANCH_NAME"'
                            sonarParams << '/d:sonar.branch.target=master'

                        } else {
                            
                            // Não sabemos o que fazer

                        }

                        def sonarParamsText = sonarParams.join(' \\\r                            ')

                        sh  """

                            export PATH="\$PATH:/root/.dotnet/tools"

                            dotnet sonarscanner begin 
                                ${sonarParamsText}

                            dotnet build --no-incremental ./Oragon.RabbitMQ.sln

                            dotnet-coverage collect "dotnet test" -f xml -o "/output-coverage/coverage.xml"

                            dotnet sonarscanner end /d:sonar.token="\$SONARQUBE_KEY"

                        """

                    }
                }
                
            }

        }

        stage('Pack') {

            agent {
                dockerfile {                    
                    args '-u root:root'
                }
            }

            when { buildingTag() }

            steps {

                script{

                    def projetcs = [
                        'Oragon.RabbitMQ',
                        'Oragon.RabbitMQ.Abstractions',
                        'Oragon.RabbitMQ.MinimalConsumer',
                        'Oragon.RabbitMQ.Serializer.NewtonsoftJson',
                        'Oragon.RabbitMQ.Serializer.SystemTextJson',
                    ]

                    if (env.BRANCH_NAME.endsWith("-alpha")) {

                        for (int i = 0; i < projetcs.size(); ++i) {
                            sh "dotnet pack ./src/${projetcs[i]}/${projetcs[i]}.csproj --configuration Debug /p:PackageVersion=${BRANCH_NAME} --include-source --include-symbols --output ./output-packages"
                        }

                    } else if (env.BRANCH_NAME.endsWith("-beta")) {

                        for (int i = 0; i < projetcs.size(); ++i) {
                            sh "dotnet pack ./src/${projetcs[i]}/${projetcs[i]}.csproj --configuration Release /p:PackageVersion=${BRANCH_NAME} --output ./output-packages"                        
                        }

                    } else {

                        for (int i = 0; i < projetcs.size(); ++i) {
                            sh "dotnet pack ./src/${projetcs[i]}/${projetcs[i]}.csproj --configuration Release /p:PackageVersion=${BRANCH_NAME} --output ./output-packages"                        
                        }

                    }

                }

            }

        }

        stage('Publish') {

            agent {
                dockerfile {                  
                    args '-u root:root'
                }
            }

            when { buildingTag() }

            steps {
                
                script {
                    
                    def publishOnNuGet = ( env.BRANCH_NAME.endsWith("-alpha") == false );
                        
                        withCredentials([usernamePassword(credentialsId: 'myget-oragon', passwordVariable: 'MYGET_KEY', usernameVariable: 'DUMMY' )]) {

                        sh 'for pkg in ./output-packages/*.nupkg ; do dotnet nuget push "$pkg" -k "$MYGET_KEY" -s https://www.myget.org/F/oragon/api/v3/index.json -ss https://www.myget.org/F/oragon/symbols/api/v2/package ; done'
						
                        }

                    if (publishOnNuGet) {

                        withCredentials([usernamePassword(credentialsId: 'nuget-luizcarlosfaria', passwordVariable: 'NUGET_KEY', usernameVariable: 'DUMMY')]) {

                            sh 'for pkg in ./output-packages/*.nupkg ; do dotnet nuget push "$pkg" -k "$NUGET_KEY" -s https://api.nuget.org/v3/index.json ; done'

                        }

                    }                    
				}
            }
        }
    }
    /*post {

        always {
            node('master'){
                
                sh  '''
                

                '''


            }
        }
    }*/
}
